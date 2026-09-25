namespace DashSpec.Core.Model;

/// <summary>Bracket layout board; optional <see cref="ModuleScope"/> when loaded from <c>.dashlayout</c>.</summary>
public sealed record LayoutBoardDefinition(
    IReadOnlyList<LayoutBoardEntry> Entries,
    LayoutScope? ModuleScope = null)
{
    public int RowCount => Entries.Count;

    public int ColumnCount
    {
        get
        {
            if (Entries.Count == 0)
            {
                return 0;
            }

            return Entries
                .Select(static entry => entry switch
                {
                    LayoutBoardCardRow cardRow => cardRow.CardIds.Count,
                    LayoutBoardGroupRow groupRow => groupRow.Group.Rows.Count == 0
                        ? 0
                        : groupRow.Group.Rows.Max(static row => row.Count),
                    _ => 0
                })
                .DefaultIfEmpty(0)
                .Max();
        }
    }

    /// <summary>Top-level card rows only (toolbar/host compat).</summary>
    public IReadOnlyList<IReadOnlyList<string>> Rows =>
        Entries
            .OfType<LayoutBoardCardRow>()
            .Select(static row => row.CardIds)
            .ToList();

    public static LayoutBoardDefinition FromCardRows(
        IReadOnlyList<IReadOnlyList<string>> rows,
        LayoutScope? moduleScope = null) =>
        new(
            rows.Select(static row => new LayoutBoardCardRow(row)).ToList(),
            moduleScope);
}
