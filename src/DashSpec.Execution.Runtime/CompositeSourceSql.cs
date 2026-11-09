using FSharpDataFlow = DashSpec.Modeling.Parse.DataFlow;

namespace DashSpec.Execution.Runtime;

/// <summary>
/// ADR-0126 slice 3: SQL composition of composite sources
/// (<c>InnerJoin/LeftJoin/RightJoin/FullJoin/UnionAll</c>). Composable
/// constituents are view / sql-query leaves — SqlFile/Xlsx cannot be composed
/// server-side and fail loudly. Join sides carry the aliases <c>l</c>/<c>r</c>
/// (the ADR: filter ownership is decided by the schema, not by branch swap);
/// union wraps into a derived table <c>u</c> so the outer query stays
/// unqualified and unambiguous.
/// </summary>
public static class CompositeSourceSql
{
    /// <summary>The FROM-body of a source: a table name, a parenthesized SQL body,
    /// an aliased join, or a union derived table. Null = the source has no SQL
    /// material (an xlsx / sql-file leaf) — callers skip it.</summary>
    public static string? FromClause(FSharpDataFlow.DashflowModule module, FSharpDataFlow.DashflowSourceDef source)
    {
        if (source.Origin is FSharpDataFlow.SourceOrigin.Leaf leaf)
        {
            return LeafFrom(leaf) is { } value ? value : null;
        }

        var left = SideOf(source, module, Side.Left);
        var right = SideOf(source, module, Side.Right);

        return source.Origin switch
        {
            FSharpDataFlow.SourceOrigin.InnerJoin => Join(left, "INNER", right, JoinCondition(source, source.Origin)),
            FSharpDataFlow.SourceOrigin.LeftJoin => Join(left, "LEFT", right, JoinCondition(source, source.Origin)),
            FSharpDataFlow.SourceOrigin.RightJoin => Join(left, "RIGHT", right, JoinCondition(source, source.Origin)),
            FSharpDataFlow.SourceOrigin.FullJoin => Join(left, "FULL", right, JoinCondition(source, source.Origin)),
            FSharpDataFlow.SourceOrigin.UnionAll =>
                $"(\nSELECT * FROM {left}\nUNION ALL\nSELECT * FROM {right}\n) AS u",
            _ => throw new InvalidOperationException(
                $"Dashflow source '{source.Id}': unsupported composite origin (ADR-0126)."),
        };
    }

    /// <summary>The reader-side query body for a composite source: a plain SELECT
    /// over the composed FROM, ready to ride the legacy card datasource (Sql/Query
    /// carrier). Leaf sources return null — they materialize as before.</summary>
    public static string? SelectBody(FSharpDataFlow.DashflowModule module, FSharpDataFlow.DashflowSourceDef source)
    {
        if (source.Origin is FSharpDataFlow.SourceOrigin.Leaf)
        {
            return null;
        }

        return $"SELECT * FROM {FromClause(module, source)}";
    }

    private static string Join(string left, string kind, string right, string condition) =>
        $"{left} AS l {kind} JOIN {right} AS r\nON {condition}";

    private static string? LeafFrom(FSharpDataFlow.SourceOrigin.Leaf leaf) =>
        leaf.kind.Equals(FSharpDataFlow.SourceFromKind.View)
            ? leaf.value
            : leaf.kind.Equals(FSharpDataFlow.SourceFromKind.SqlQuery)
                ? $"(\n{leaf.value}\n)"
                : null;

    private static string SideOf(
        FSharpDataFlow.DashflowSourceDef source,
        FSharpDataFlow.DashflowModule module,
        Side side)
    {
        var constituentId = side switch
        {
            Side.Left => LeftOf(source.Origin),
            _ => RightOf(source.Origin),
        };

        var constituent = module.Sources.FirstOrDefault(s =>
            string.Equals(s.Id, constituentId, StringComparison.OrdinalIgnoreCase));

        if (constituent is null || constituent.Origin is not FSharpDataFlow.SourceOrigin.Leaf leaf)
        {
            throw new InvalidOperationException(
                $"Dashflow composite source '{source.Id}': {(side == Side.Left ? "left" : "right")} side references unknown or non-leaf source '{constituentId}' (ADR-0126 §1).");
        }

        if (LeafFrom(leaf) is not { } value)
        {
            throw new InvalidOperationException(
                $"Dashflow composite source '{source.Id}': {(side == Side.Left ? "left" : "right")} side '{constituentId}' is a sql-file/xlsx leaf — only view and sql-query leaves compose server-side (ADR-0126 §1).");
        }

        return value;
    }

    private static string LeftOf(FSharpDataFlow.SourceOrigin origin) => origin switch
    {
        FSharpDataFlow.SourceOrigin.InnerJoin j => j.left,
        FSharpDataFlow.SourceOrigin.LeftJoin j => j.left,
        FSharpDataFlow.SourceOrigin.RightJoin j => j.left,
        FSharpDataFlow.SourceOrigin.FullJoin j => j.left,
        FSharpDataFlow.SourceOrigin.UnionAll u => u.left,
        _ => throw new InvalidOperationException("unreachable — composite origin checked by caller"),
    };

    private static string RightOf(FSharpDataFlow.SourceOrigin origin) => origin switch
    {
        FSharpDataFlow.SourceOrigin.InnerJoin j => j.right,
        FSharpDataFlow.SourceOrigin.LeftJoin j => j.right,
        FSharpDataFlow.SourceOrigin.RightJoin j => j.right,
        FSharpDataFlow.SourceOrigin.FullJoin j => j.right,
        FSharpDataFlow.SourceOrigin.UnionAll u => u.right,
        _ => throw new InvalidOperationException("unreachable — composite origin checked by caller"),
    };

    private static string JoinCondition(FSharpDataFlow.DashflowSourceDef source, FSharpDataFlow.SourceOrigin origin)
    {
        var cond = origin switch
        {
            FSharpDataFlow.SourceOrigin.InnerJoin j => j.cond,
            FSharpDataFlow.SourceOrigin.LeftJoin j => j.cond,
            FSharpDataFlow.SourceOrigin.RightJoin j => j.cond,
            FSharpDataFlow.SourceOrigin.FullJoin j => j.cond,
            _ => throw new InvalidOperationException("unreachable — join origin checked by caller"),
        };

        if (cond.On is { IsEmpty: false } on)
        {
            return string.Join("\n  AND ", on.Select(key => $"l.{key} = r.{key}"));
        }

        if (cond.LeftKey is { IsEmpty: false } leftKey && cond.RightKey is { IsEmpty: false } rightKey)
        {
            if (leftKey.Length != rightKey.Length)
            {
                throw new InvalidOperationException(
                    $"Dashflow composite source '{source.Id}': left_key ({leftKey.Length}) and right_key ({rightKey.Length}) lists differ in length (ADR-0126 §1).");
            }

            return string.Join("\n  AND ", leftKey.Zip(rightKey, (l, r) => $"l.{l} = r.{r}"));
        }

        throw new InvalidOperationException(
            $"Dashflow composite source '{source.Id}': join needs 'on' or paired 'left_key'/'right_key' (ADR-0126 §1).");
    }

    private enum Side
    {
        Left,
        Right,
    }
}