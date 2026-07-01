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

        var ordered = new List<(string Label, double? Value)>();
        var indexByLabel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var label = PayloadRowFormatters.FormatValue(row.GetValueOrDefault(xColumn));
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            var value = PayloadRowFormatters.ToDouble(row.GetValueOrDefault(yColumn));
            if (indexByLabel.TryGetValue(label, out var existingIndex))
            {
                var existing = ordered[existingIndex].Value;
                if (value is not null && (existing is null || value > existing))
                {
                    ordered[existingIndex] = (label, value);
                }

                continue;
            }

            indexByLabel[label] = ordered.Count;
            ordered.Add((label, value));
        }

        var labels = ordered.Select(x => x.Label).ToList();
        var values = ordered.Select(x => x.Value).ToList();
        var pointColors = ChartColorResolver.ResolveLabelColors(labels, card, library, dashboardColorPalette);

        var payload = ApplyMaxSeries(
            new ChartPayload(labels, [new ChartSeries("default", values, PointColors: pointColors)]),
            seriesTransform);

        return payload;
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
            return new ChartPayload(payload.Labels, keep);
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
        return new ChartPayload(payload.Labels, keep);
    }
}
