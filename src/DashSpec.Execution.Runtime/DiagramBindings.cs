
namespace DashSpec.Execution.Runtime;

public static class DiagramBindings
{
    public static string Column(DiagramDefinition diagram, string role) =>
        TryGetColumn(diagram, role, out var column)
            ? column
            : throw new InvalidOperationException(
                $"Diagram requires binding for '{role}' ({DescribeExpected(role, diagram.Kind)}).");

    public static bool TryGetColumn(DiagramDefinition diagram, string role, out string column)
    {
        foreach (var key in PropertyKeysForRole(diagram.Kind, role))
        {
            if (diagram.Properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                column = value;
                return true;
            }
        }

        column = string.Empty;
        return false;
    }

    public static IEnumerable<string> SelectColumnRoles(string? kind) =>
        kind?.ToLowerInvariant() switch
        {
            "bar" or "pie" or "donut" or "doughnut" or "treemap" or "windrose" or "wind_rose" => ["x", "y", "reference", "series", "color"],
            "line" or "area" or "sparkline" => ["x", "y", "series"],
            "scatter" => ["x", "y", "size"],
            "histogram" => ["value", "x"],
            "box" or "boxplot" => ["value", "x"],
            "gauge" => ["value"],
            "heatmap" => ["x", "y", "value"],
            "gantt" => ["y", "from", "to", "color"],
            _ => ["x", "y", "series", "value"],
        };

    /// <summary>Roles a diagram kind cannot render without — from the kind registry
    /// (Modeling.Parse SSOT). A kind without a registry spec declares no mandatory
    /// bindings; the renderer stays the loud backstop.</summary>
    public static IReadOnlyList<string> RequiredRoles(string? kind) =>
        [.. DashSpec.Modeling.Parse.Diagram.DiagramKindRegistry.requiredRoles(kind ?? string.Empty)];

    /// <summary>Compile-time contract check: every required role must have a bound
    /// column. Throws DashSpecFlowException with the card context on the first gap.</summary>
    public static void ValidateRequiredBindings(string context, DiagramDefinition diagram)
    {
        foreach (var role in RequiredRoles(diagram.Kind))
        {
            if (!TryGetColumn(diagram, role, out _))
            {
                throw new DashSpec.Modeling.Core.DashSpecFlowException(
                    $"{context}: diagram kind '{diagram.Kind}' requires a binding for '{role}' " +
                    $"({DescribeExpected(role, diagram.Kind)}).");
            }
        }
    }

    public static IEnumerable<string> SelectedSqlColumns(
        DiagramDefinition diagram,
        TooltipDefinition? tooltip = null)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in SelectColumnRoles(diagram.Kind))
        {
            if (TryGetColumn(diagram, role, out var column))
            {
                names.Add(column);
            }
        }

        if (tooltip is not null)
        {
            foreach (var column in TooltipTemplate.SelectColumns(tooltip))
            {
                names.Add(column);
            }
        }

        return names;
    }

    public static string? Label(DiagramDefinition diagram, string role)
    {
        foreach (var key in PropertyKeysForRole(diagram.Kind, role))
        {
            if (diagram.Properties.TryGetValue(LabelKey(key), out var label) &&
                !string.IsNullOrWhiteSpace(label))
            {
                return label;
            }
        }

        return null;
    }

    public static string LabelKey(string key) => $"{key}_as";

    private static IEnumerable<string> PropertyKeysForRole(string? kind, string role)
    {
        var r = role.ToLowerInvariant();

        // Line-family charts speak the same category/value dialect as category
        // charts (the line DSL block accepts category=/value= and the QueryCompiler
        // groups by them) — the role lookup must accept both spellings, or the
        // render dies on a diagram the compiler happily accepted.
        if (kind?.ToLowerInvariant() is "line" or "area" or "sparkline" or "scatter")
        {
            return r switch
            {
                "x" => ["x", "category"],
                "y" => ["y", "value", "measure"],
                _ => [role],
            };
        }

        if (IsCategoryChart(kind))
        {
            return r switch
            {
                "x" or "category" => ["category", "x"],
                "y" or "value" or "measure" => ["value", "measure", "y"],
                "reference" => ["reference"],
                _ => [role],
            };
        }

        return [role];
    }

    public static bool IsCategoryChart(string? kind) =>
        kind?.ToLowerInvariant() is "bar" or "pie" or "donut" or "doughnut" or "treemap"
            or "windrose" or "wind_rose";

    public static bool IsRadialChart(string? kind) =>
        kind?.ToLowerInvariant() is "pie" or "donut" or "doughnut" or "windrose" or "wind_rose";

    private static string DescribeExpected(string role, string? kind) =>
        IsCategoryChart(kind) && role is "x" or "y"
            ? $"{kind}: category/value or x/y"
            : "see diagram bindings";
}
