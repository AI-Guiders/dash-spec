using DashSpec.Core.Model;

namespace DashSpec.Core.Runtime;

public sealed record ChartPresentation(
    string Legend = "bottom",
    int HeightPx = 280,
    int? MaxSeries = null,
    bool Stacked = false)
{
    public static ChartPresentation FromDiagram(DiagramDefinition diagram)
    {
        var legend = diagram.Properties.TryGetValue("legend", out var rawLegend)
            ? rawLegend.ToLowerInvariant()
            : "bottom";

        var height = 280;
        if (diagram.Properties.TryGetValue("height", out var rawHeight) &&
            int.TryParse(rawHeight, out var parsedHeight) &&
            parsedHeight is >= 120 and <= 800)
        {
            height = parsedHeight;
        }

        int? maxSeries = null;
        if (diagram.Properties.TryGetValue("max_series", out var rawMax) &&
            int.TryParse(rawMax, out var parsedMax) &&
            parsedMax > 0)
        {
            maxSeries = parsedMax;
        }

        var stacked = diagram.Properties.TryGetValue("stacked", out var rawStacked) &&
                      rawStacked is "true" or "yes" or "1";

        return new ChartPresentation(legend, height, maxSeries, stacked);
    }
}
