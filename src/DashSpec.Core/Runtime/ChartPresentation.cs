using DashSpec.Core.Model;

namespace DashSpec.Core.Runtime;

public sealed record ChartPresentation(
    string Legend = "bottom",
    int HeightPx = 280,
    int? MaxSeries = null,
    bool Stacked = false,
    ChartOrientation Orientation = ChartOrientation.Vertical,
    ChartAxisScale ValueAxisScale = ChartAxisScale.Decimal)
{
    public bool IsHorizontal => Orientation is ChartOrientation.Horizontal;

    public static ChartPresentation FromProperties(
        IReadOnlyDictionary<string, string> properties,
        int? maxSeries = null)
    {
        var legend = properties.TryGetValue("legend", out var rawLegend)
            ? rawLegend.ToLowerInvariant()
            : "bottom";

        var height = 280;
        if (properties.TryGetValue("height", out var rawHeight) &&
            int.TryParse(rawHeight, out var parsedHeight) &&
            parsedHeight is >= 120 and <= 800)
        {
            height = parsedHeight;
        }

        int? resolvedMaxSeries = maxSeries;
        if (resolvedMaxSeries is null &&
            properties.TryGetValue("max_series", out var rawMax) &&
            int.TryParse(rawMax, out var parsedMax) &&
            parsedMax > 0)
        {
            resolvedMaxSeries = parsedMax;
        }

        var stacked = properties.TryGetValue("stacked", out var rawStacked) &&
                      rawStacked is "true" or "yes" or "1";

        var orientation = ChartOrientationParser.Parse(
            properties.GetValueOrDefault("orientation"),
            ChartOrientation.Vertical);

        var valueAxisScale = ChartAxisScaleParser.ResolveValueAxis(properties);

        return new ChartPresentation(legend, height, resolvedMaxSeries, stacked, orientation, valueAxisScale);
    }

    public static ChartPresentation FromDiagram(DiagramDefinition diagram) =>
        FromProperties(diagram.Properties);
}
