
using DashSpec.Modeling.Parse.Layout;

namespace DashSpec.Execution.Runtime;

/// <summary>Toolbar grid placement from board or legacy flat filter list.</summary>
public static class ToolbarLayoutCompactor
{
    public static IReadOnlyDictionary<string, PlacementDefinition> Compact(DashboardDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var columns = document.Layout.Columns;
        if (document.ToolbarBoard?.Value is not null)
        {
            var board = document.ToolbarBoard.Value;
            // The scope module's own grid drawing sets the tracks for its placements
            // ([ADR-0111] §2 recursion); the document grid is the default.
            var boardColumns = board.GridColumns > 0 ? board.GridColumns : columns;
            var layout = new Dictionary<string, PlacementDefinition>(
                LayoutBoardPlacer.Resolve(
                    board,
                    boardColumns,
                    "Toolbar",
                    token => FilterLayoutRefBridge.Resolve(token, document.Filters, "Toolbar")),
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

        var column = 1;
        foreach (var (name, span) in FallbackSpans(document, filters, columns))
        {
            result[name] = new PlacementDefinition(1, column, span);
            column += span;
        }

        return result;
    }

    /// <summary>Fallback toolbar geometry: the even split, except a date-range filter takes
    /// two extra columns — «dd.MM.yyyy — dd.MM.yyyy» cannot wrap inside an even share (two
    /// date inputs plus separator need ~250px). The borrow is paid back by the row tail
    /// (min span 1); when even that doesn't fit the grid, the plain even split stands.</summary>
    internal static IReadOnlyList<(string Name, int Span)> FallbackSpans(
        DashboardDocument document,
        IReadOnlyList<string> names,
        int columns)
    {
        var count = names.Count;
        var even = count == 1 ? columns : Math.Max(1, columns / count);
        var spans = new int[count];
        Array.Fill(spans, even);
        var isDateRange = new bool[count];
        var dateRanges = 0;
        for (var i = 0; i < count; i++)
        {
            isDateRange[i] = document.Filters.FirstOrDefault(filter =>
                    string.Equals(filter.Name, names[i], StringComparison.OrdinalIgnoreCase)) is { } filter
                && filter.Kind == FilterKind.Date
                && !filter.IsDayWidget;
            if (isDateRange[i])
            {
                dateRanges++;
            }
        }

        if (dateRanges == 0)
        {
            return spans.Select((span, i) => (names[i], span)).ToList();
        }

        const int dateBorrow = 2;
        var total = even * count;
        for (var i = 0; i < count; i++)
        {
            if (!isDateRange[i])
            {
                continue;
            }

            spans[i] += dateBorrow;
            total += dateBorrow;
        }

        for (var i = count - 1; i >= 0 && total > columns; i--)
        {
            if (isDateRange[i] || spans[i] <= 1)
            {
                continue;
            }

            spans[i] -= 1;
            total--;
        }

        if (total > columns)
        {
            // The borrow never fits (many dates, few columns) — the even split stands.
            Array.Fill(spans, even);
        }

        return spans.Select((span, i) => (names[i], span)).ToList();
    }

    /// <summary>Toolbar placement for a subset of filters; empty board rows are omitted.</summary>
    public static IReadOnlyDictionary<string, PlacementDefinition> CompactVisible(
        DashboardDocument document,
        IReadOnlySet<string> visibleFilterNames,
        LayoutBoardDefinition? activeToolbarBoard = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(visibleFilterNames);

        if (visibleFilterNames.Count == 0)
        {
            return new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase);
        }

        var columns = document.Layout.Columns;
        var board = activeToolbarBoard ?? document.ToolbarBoard?.Value;
        if (board is not null)
        {
            // The scope module's own grid drawing sets the tracks for its placements
            // ([ADR-0111] §2 recursion); the document grid is the default.
            return CompactVisibleBoard(
                board,
                board.GridColumns > 0 ? board.GridColumns : columns,
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
                var (refToken, _) = LayoutBoardCellParser.parseCell(token, "Toolbar", compactedRow + 1);

                // Rubber cells survive compaction: PlaceRow turns them into gaps ([ADR-0111] §2).
                if (refToken == "[]")
                {
                    visibleInRow.Add(refToken);
                    continue;
                }

                var filterName = FilterLayoutRefBridge.Resolve(refToken, filters, "Toolbar");
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
