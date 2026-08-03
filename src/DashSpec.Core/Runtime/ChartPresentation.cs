using DashSpec.Core.Model;

namespace DashSpec.Core.Runtime;

public sealed record ChartPresentation(
    string Legend = "bottom",
    int HeightPx = 280,
    int? MaxSeries = null,
    bool Stacked = false,
    ChartOrientation Orientation = ChartOrientation.Vertical,
    ChartAxisScale ValueAxisScale = ChartAxisScale.Decimal,
    string? CategoryAxisLabel = null,
    string? ValueAxisLabel = null,
    double? ValueAxisMax = null)
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

        double? valueAxisMax = null;
        if (TryReadAxisMax(properties, "y_max", out var yMax) ||
            TryReadAxisMax(properties, "value_axis_max", out yMax))
        {
            valueAxisMax = yMax;
        }
        else if (valueAxisScale is ChartAxisScale.Percent)
        {
            valueAxisMax = 100;
        }

        return new ChartPresentation(
            legend,
            height,
            resolvedMaxSeries,
            stacked,
            orientation,
            valueAxisScale,
            ValueAxisMax: valueAxisMax);
    }

    private static bool TryReadAxisMax(
        IReadOnlyDictionary<string, string> properties,
        string key,
        out double value)
    {
        value = 0;
        if (!properties.TryGetValue(key, out var raw) ||
            !double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        return value > 0;
    }

    public static ChartPresentation FromDiagram(DiagramDefinition diagram) =>
        FromProperties(diagram.Properties);
}
