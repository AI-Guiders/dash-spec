using DashSpec.Core.Model;

namespace DashSpec.Core.Runtime;

internal static class ChartSeriesPayloadBuilder
{
    public static ChartPayload Build(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        DiagramDefinition diagram,
        SeriesTransformSettings? seriesTransform = null)
    {
        var xColumn = DiagramBindings.Column(diagram, "x");
        var yColumn = DiagramBindings.Column(diagram, "y");
        diagram.Properties.TryGetValue("series", out var seriesColumn);

        var labels = new List<string>();
        var labelSet = new HashSet<string>(StringComparer.Ordinal);
        var datasets = new Dictionary<string, List<double?>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var x = PayloadRowFormatters.FormatValue(row.GetValueOrDefault(xColumn));
            if (labelSet.Add(x))
            {
                labels.Add(x);
            }

            var seriesKey = seriesColumn is null
                ? "default"
                : PayloadRowFormatters.FormatValue(row.GetValueOrDefault(seriesColumn));

            if (!datasets.TryGetValue(seriesKey, out var values))
            {
                values = labels.Select(_ => (double?)null).ToList();
                datasets[seriesKey] = values;
            }

            while (values.Count < labels.Count)
            {
                values.Add(null);
            }

            var index = labels.IndexOf(x);
            values[index] = PayloadRowFormatters.ToDouble(row.GetValueOrDefault(yColumn));
        }

        var payload = new ChartPayload(
            labels,
            datasets.Select(x => new ChartSeries(x.Key, x.Value)).ToList());

        return ApplyMaxSeries(payload, seriesTransform);
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
