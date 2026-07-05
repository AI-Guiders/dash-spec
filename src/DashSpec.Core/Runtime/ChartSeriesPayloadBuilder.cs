using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Runtime;

internal static class ChartSeriesPayloadBuilder
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
        diagram.Properties.TryGetValue("series", out var seriesColumn);
        diagram.Properties.TryGetValue("x_format", out var xFormat);
        diagram.Properties.TryGetValue("x_step", out var xStepRaw);
        var useTimeGrid = TimeSeriesGrid.TryParseStep(xStepRaw, out var xStep);

        var buckets = new SortedDictionary<DateTime, Dictionary<string, double?>>(Comparer<DateTime>.Default);

        foreach (var row in rows)
        {
            var bucket = TimeSeriesGrid.TryParseBucket(row.GetValueOrDefault(xColumn));
            if (bucket is null)
            {
                continue;
            }

            var xKey = useTimeGrid ? TimeSeriesGrid.Floor(bucket.Value, xStep) : bucket.Value;
            if (!buckets.TryGetValue(xKey, out var seriesValues))
            {
                seriesValues = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);
                buckets[xKey] = seriesValues;
            }

            var seriesKey = seriesColumn is null
                ? "default"
                : PayloadRowFormatters.FormatValue(row.GetValueOrDefault(seriesColumn));

            seriesValues[seriesKey] = PayloadRowFormatters.ToDouble(row.GetValueOrDefault(yColumn));
        }

        if (useTimeGrid && buckets.Count > 0)
        {
            buckets = ExpandGrid(buckets, xStep);
        }

        var keys = buckets.Keys.ToList();
        var labels = keys
            .Select(key => PayloadRowFormatters.FormatChartAxisLabel(key, xFormat))
            .ToList();

        var datasets = new Dictionary<string, List<double?>>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < keys.Count; index++)
        {
            foreach (var (seriesKey, value) in buckets[keys[index]])
            {
                if (!datasets.TryGetValue(seriesKey, out var values))
                {
                    values = keys.Select(_ => (double?)null).ToList();
                    datasets[seriesKey] = values;
                }

                values[index] = value;
            }
        }

        var payload = ApplyMaxSeries(
            new ChartPayload(labels, datasets.Select(x => new ChartSeries(x.Key, x.Value)).ToList()),
            seriesTransform);

        return payload with
        {
            Series = ChartColorResolver.ApplySeriesColors(payload.Series, card, library, dashboardColorPalette),
        };
    }

    private static SortedDictionary<DateTime, Dictionary<string, double?>> ExpandGrid(
        SortedDictionary<DateTime, Dictionary<string, double?>> buckets,
        TimeSpan step)
    {
        var min = buckets.Keys.First();
        var max = buckets.Keys.Last();
        var expanded = new SortedDictionary<DateTime, Dictionary<string, double?>>(Comparer<DateTime>.Default);

        foreach (var bucket in TimeSeriesGrid.Range(min, max, step))
        {
            expanded[bucket] = buckets.TryGetValue(bucket, out var values)
                ? new Dictionary<string, double?>(values, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);
        }

        return expanded;
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
