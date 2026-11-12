

namespace DashSpec.Execution.Runtime;

public enum TableWidthMode
{
    Content,
    Fill,
}

public enum TableDensity
{
    Comfortable,
    Compact,
}

public sealed record TablePresentation(
    TableWidthMode Width = TableWidthMode.Content,
    TableDensity Density = TableDensity.Comfortable,
    int MaxHeightPx = 420,
    bool ColumnFilters = true,
    int FrozenColumns = 0,
    bool AllowGrouping = false,
    int PageSize = 15)
{
    public static TablePresentation FromProperties(IReadOnlyDictionary<string, string> properties)
    {
        var width = TableWidthMode.Content;
        if (properties.TryGetValue("width", out var rawWidth))
        {
            width = rawWidth.ToLowerInvariant() switch
            {
                "fill" or "100%" or "full" or "stretch" => TableWidthMode.Fill,
                _ => TableWidthMode.Content,
            };
        }

        var density = TableDensity.Comfortable;
        if (properties.TryGetValue("density", out var rawDensity) &&
            rawDensity.Equals("compact", StringComparison.OrdinalIgnoreCase))
        {
            density = TableDensity.Compact;
        }

        var maxHeight = 420;
        if (properties.TryGetValue("height", out var rawHeight) &&
            int.TryParse(rawHeight, out var parsedHeight) &&
            parsedHeight > 0)
        {
            maxHeight = parsedHeight;
        }
        else if (properties.TryGetValue("max_height", out var rawMaxHeight) &&
                 int.TryParse(rawMaxHeight, out var parsedMax) &&
                 parsedMax > 0)
        {
            maxHeight = parsedMax;
        }

        var columnFilters = true;
        if (properties.TryGetValue("column_filters", out var rawFilters))
        {
            columnFilters = rawFilters is "true" or "yes" or "1";
        }

        // Frozen columns (ADR-0115 §2): author intent — keep the first N columns pinned
        // during horizontal scroll. Radzen honors it; the self-made table ignores it.
        var frozenColumns = 0;
        if (properties.TryGetValue("frozen_columns", out var rawFrozen) &&
            int.TryParse(rawFrozen.Trim(), out var parsedFrozen))
        {
            frozenColumns = Math.Clamp(parsedFrozen, 0, 8);
        }

        // Interactive grouping (the blocked table-grouping feature, unblocked by ADR-0121's
        // carrier): author intent knob — the Radzen grid turns it into AllowGrouping
        // (drag-a-column-to-group, in-memory over the loaded page); the self-made table ignores it.
        var allowGrouping = false;
        if (properties.TryGetValue("grouping", out var rawGrouping))
        {
            allowGrouping = rawGrouping is "true" or "yes" or "1" or "on";
        }

        var pageSize = 15;
        if (properties.TryGetValue("page_size", out var rawPageSize) &&
            int.TryParse(rawPageSize.Trim(), out var parsedPageSize) &&
            parsedPageSize > 0)
        {
            pageSize = parsedPageSize;
        }

        return new TablePresentation(width, density, maxHeight, columnFilters, frozenColumns, allowGrouping, pageSize);
    }

    public static TablePresentation Resolve(CardDefinition card, SpecLibrary? library) =>
        FromProperties(ChartChromeProperties.Merge(card, library));
}
