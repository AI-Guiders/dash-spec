using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Runtime;

internal static class CategoryChartPayloadBuilder
{
    public static ChartPayload Build(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        DiagramDefinition diagram,
        SeriesTransformSettings? seriesTransform,
        CardDefinition card,
        SpecLibrary? library,
        string? dashboardColorPalette = null)
    {
        var xColumn = DiagramBindings.Column(diagram, "x");
        var yColumn = DiagramBindings.Column(diagram, "y");
        var hasReference = DiagramBindings.TryGetColumn(diagram, "reference", out var referenceColumn);
        var referenceLabel = DiagramBindings.Label(diagram, "reference") ?? "Куплено";

        var ordered = new List<(string Label, double? Value, double? Reference)>();
        var indexByLabel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var label = PayloadRowFormatters.FormatValue(row.GetValueOrDefault(xColumn));
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            var value = PayloadRowFormatters.ToDouble(row.GetValueOrDefault(yColumn));
            var reference = hasReference
                ? PayloadRowFormatters.ToDouble(row.GetValueOrDefault(referenceColumn))
                : null;

            if (indexByLabel.TryGetValue(label, out var existingIndex))
            {
                var existing = ordered[existingIndex].Value;
                if (value is not null && (existing is null || value > existing))
                {
                    ordered[existingIndex] = (label, value, reference ?? ordered[existingIndex].Reference);
                }

                continue;
            }

            indexByLabel[label] = ordered.Count;
            ordered.Add((label, value, reference));
        }

        var labels = ordered.Select(x => x.Label).ToList();
        var values = ordered.Select(x => x.Value).ToList();
        var references = hasReference
            ? ordered.Select(x => x.Reference).ToList()
            : null;
        var pointColors = ResolveBarPointColors(
            labels,
            values,
            references,
            diagram,
            card,
            library,
            dashboardColorPalette);

        var payload = ApplyMaxSeries(
            new ChartPayload(
                labels,
                [new ChartSeries("default", values, PointColors: pointColors)],
                references,
                hasReference ? referenceLabel : null),
            seriesTransform);

        return payload;
    }

    private static IReadOnlyList<string>? ResolveBarPointColors(
        IReadOnlyList<string> labels,
        IReadOnlyList<double?> values,
        IReadOnlyList<double?>? references,
        DiagramDefinition diagram,
        CardDefinition card,
        SpecLibrary? library,
        string? dashboardColorPalette)
    {
        const string calmBar = "#60a5fa";
        const string overPercentBar = "#ef4444";
        var percentCap = ChartChromeProperties.TryGetPercentCap(card, library, out var cap)
            ? cap
            : TryReadPercentCap(diagram.Properties);
        var paletteColors = ChartColorResolver.ResolveLabelColors(labels, card, library, dashboardColorPalette);
        var colors = new string[labels.Count];

        for (var i = 0; i < labels.Count; i++)
        {
            var value = values[i];

            if (references is not null && references.Count == values.Count)
            {
                colors[i] = calmBar;
                continue;
            }

            if (percentCap.HasValue && value is { } v && v > percentCap.Value)
            {
                colors[i] = overPercentBar;
                continue;
            }

            colors[i] = paletteColors is not null && i < paletteColors.Count
                ? paletteColors[i]
                : calmBar;
        }

        return colors;
    }

    private static double? TryReadPercentCap(IReadOnlyDictionary<string, string> properties)
    {
        if (properties.TryGetValue("y_max", out var raw) &&
            double.TryParse(
                raw,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed) &&
            parsed > 0)
        {
            return parsed;
        }

        return null;
    }

    private static ChartPayload ApplyMaxSeries(ChartPayload payload, SeriesTransformSettings? transform)
    {
        if (transform is null || payload.Series.Count <= transform.Max)
        {
            return payload;
        }

        var maxSeries = transform.Max;
        var otherLabel = transform.OtherLabel;

        var ranked = payload.Series
            .Select(series => (series, Total: series.Values.Sum(v => v ?? 0)))
            .OrderByDescending(x => x.Total)
            .ToList();

        var keep = ranked.Take(maxSeries - 1).Select(x => x.series).ToList();
        var rest = ranked.Skip(maxSeries - 1).ToList();
        if (rest.Count == 0)
        {
            return new ChartPayload(payload.Labels, keep, payload.ReferenceValues, payload.ReferenceLabel);
        }

        var otherValues = new double?[payload.Labels.Count];
        foreach (var (series, _) in rest)
        {
            for (var i = 0; i < payload.Labels.Count; i++)
            {
                var value = series.Values.ElementAtOrDefault(i);
                if (value is null)
                {
                    continue;
                }

                otherValues[i] = (otherValues[i] ?? 0) + value.Value;
            }
        }

        keep.Add(new ChartSeries(otherLabel, otherValues));
        return new ChartPayload(payload.Labels, keep, payload.ReferenceValues, payload.ReferenceLabel);
    }
}
