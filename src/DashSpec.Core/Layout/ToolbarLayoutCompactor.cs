using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Layout;

/// <summary>Toolbar grid placement from board or legacy flat filter list.</summary>
public static class ToolbarLayoutCompactor
{
    public static IReadOnlyDictionary<string, PlacementDefinition> Compact(DashboardDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var columns = document.Layout.Columns;
        if (document.ToolbarBoard is not null)
        {
            return LayoutBoardPlacer.Resolve(
                document.ToolbarBoard,
                columns,
                "Toolbar",
                token => FilterLayoutRefResolver.Resolve(token, document.Filters, "Toolbar"));
        }

        var result = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase);
        var filters = document.DashboardFilters;
        if (filters.Count == 0)
        {
            return result;
        }

        var cellCount = filters.Count;
        var span = cellCount == 1 ? columns : columns / cellCount;
        for (var i = 0; i < cellCount; i++)
        {
            result[filters[i]] = new PlacementDefinition(1, 1 + i * span, span);
        }

        return result;
    }
}
