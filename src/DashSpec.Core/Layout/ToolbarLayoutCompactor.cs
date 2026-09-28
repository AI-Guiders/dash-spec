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
            var layout = new Dictionary<string, PlacementDefinition>(
                LayoutBoardPlacer.Resolve(
                    document.ToolbarBoard,
                    columns,
                    "Toolbar",
                    token => FilterLayoutRefResolver.Resolve(token, document.Filters, "Toolbar")),
                StringComparer.OrdinalIgnoreCase);
            LayoutPlacementResolution.ApplyFilterPlacementOverrides(document.Filters, layout);
            return layout;
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

    /// <summary>Toolbar placement for a subset of filters; empty board rows are omitted.</summary>
    public static IReadOnlyDictionary<string, PlacementDefinition> CompactVisible(
        DashboardDocument document,
        IReadOnlySet<string> visibleFilterNames)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(visibleFilterNames);

        if (visibleFilterNames.Count == 0)
        {
            return new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase);
        }

        var columns = document.Layout.Columns;
        if (document.ToolbarBoard is not null)
        {
            return CompactVisibleBoard(
                document.ToolbarBoard,
                columns,
                document.Filters,
                visibleFilterNames);
        }

        var result = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase);
        var visible = document.DashboardFilters
            .Where(visibleFilterNames.Contains)
            .ToList();
        if (visible.Count == 0)
        {
            return result;
        }

        LayoutBoardRowPlacer.PlaceRow(
            visible,
            1,
            columns,
            "Toolbar",
            filterName => filterName,
            result);
        LayoutPlacementResolution.ApplyFilterPlacementOverrides(
            document.Filters.Where(f => visible.Contains(f.Name)).ToList(),
            result);
        return result;
    }

    private static IReadOnlyDictionary<string, PlacementDefinition> CompactVisibleBoard(
        LayoutBoardDefinition board,
        int columns,
        IReadOnlyList<FilterDefinition> filters,
        IReadOnlySet<string> visibleFilterNames)
    {
        var result = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase);
        var compactedRow = 0;

        foreach (var row in board.Rows)
        {
            var visibleInRow = new List<string>();
            foreach (var token in row)
            {
                var cell = LayoutBoardRowPlacer.ParseCell(token, "Toolbar", compactedRow + 1);
                var filterName = FilterLayoutRefResolver.Resolve(cell.RefToken, filters, "Toolbar");
                if (visibleFilterNames.Contains(filterName))
                {
                    visibleInRow.Add(filterName);
                }
            }

            if (visibleInRow.Count == 0)
            {
                continue;
            }

            compactedRow++;
            LayoutBoardRowPlacer.PlaceRow(
                visibleInRow,
                compactedRow,
                columns,
                "Toolbar",
                filterName => filterName,
                result);
        }

        LayoutPlacementResolution.ApplyFilterPlacementOverrides(
            filters.Where(f => result.ContainsKey(f.Name)).ToList(),
            result);
        return result;
    }
}
