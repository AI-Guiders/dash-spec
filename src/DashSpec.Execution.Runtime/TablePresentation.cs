using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

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
    bool ColumnFilters = true)
{
    public static TablePresentation FromProperties(IReadOnlyDictionary<string, string> properties)
    {
        var width = TableWidthMode.Content;
        if (properties.TryGetValue("width", out var rawWidth))
        {
            width = rawWidth.ToLowerInvariant() switch
            {
                "fill" or "100%" or "full" => TableWidthMode.Fill,
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
            parsedHeight is >= 120 and <= 800)
        {
            maxHeight = parsedHeight;
        }
        else if (properties.TryGetValue("max_height", out var rawMaxHeight) &&
                 int.TryParse(rawMaxHeight, out var parsedMax) &&
                 parsedMax is >= 120 and <= 800)
        {
            maxHeight = parsedMax;
        }

        var columnFilters = true;
        if (properties.TryGetValue("column_filters", out var rawFilters))
        {
            columnFilters = rawFilters is "true" or "yes" or "1";
        }

        return new TablePresentation(width, density, maxHeight, columnFilters);
    }

    public static TablePresentation Resolve(CardDefinition card, SpecLibrary? library) =>
        FromProperties(ChartChromeProperties.Merge(card, library));
}
