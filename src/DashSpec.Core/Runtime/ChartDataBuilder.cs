using DashSpec.Core.Model;

namespace DashSpec.Core.Runtime;

public static class ChartDataBuilder
{
    public static ChartPayload BuildLineOrBar(
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
            var x = FormatValue(row.GetValueOrDefault(xColumn));
            if (labelSet.Add(x))
            {
                labels.Add(x);
            }

            var seriesKey = seriesColumn is null
                ? "default"
                : FormatValue(row.GetValueOrDefault(seriesColumn));

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
            values[index] = ToDouble(row.GetValueOrDefault(yColumn));
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

    public static TablePayload BuildTable(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows, DiagramDefinition diagram)
    {
        var columns = diagram.Properties.TryGetValue("columns", out var raw)
            ? raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            : rows.FirstOrDefault()?.Keys.ToArray() ?? [];

        var tableRows = rows.Select(row =>
            columns.Select(column => FormatValue(row.GetValueOrDefault(column))).ToList()).ToList();

        return new TablePayload(columns.ToList(), tableRows);
    }

    public static MatrixPayload BuildHeatmap(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        DiagramDefinition diagram)
    {
        var xColumn = DiagramBindings.Column(diagram, "x");
        var yColumn = DiagramBindings.Column(diagram, "y");
        var valueColumn = DiagramBindings.Column(diagram, "value");
        diagram.Properties.TryGetValue("tooltip", out var tooltipColumn);
        diagram.Properties.TryGetValue("y_format", out var yFormat);
        diagram.Properties.TryGetValue("tooltip_split", out var rawTooltipSplit);
        var tooltipSplit = string.IsNullOrWhiteSpace(rawTooltipSplit) ? ", " : rawTooltipSplit;

        var xLabels = new List<string>();
        var xIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var yLabels = new List<string>();
        var yIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var yTotals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var x = FormatHeatmapLabel(row.GetValueOrDefault(xColumn));
            var y = FormatHeatmapAxisLabel(row.GetValueOrDefault(yColumn), yFormat);
            if (string.IsNullOrEmpty(x) || string.IsNullOrEmpty(y))
            {
                continue;
            }

            if (!xIndex.ContainsKey(x))
            {
                xIndex[x] = xLabels.Count;
                xLabels.Add(x);
            }

            if (!yIndex.ContainsKey(y))
            {
                yIndex[y] = yLabels.Count;
                yLabels.Add(y);
            }

            var value = ToDouble(row.GetValueOrDefault(valueColumn)) ?? 0;
            yTotals[y] = yTotals.GetValueOrDefault(y) + value;
        }

        yLabels.Sort((a, b) => yTotals.GetValueOrDefault(b).CompareTo(yTotals.GetValueOrDefault(a)));
        yIndex.Clear();
        for (var i = 0; i < yLabels.Count; i++)
        {
            yIndex[yLabels[i]] = i;
        }

        SortHeatmapXLabels(xLabels);

        xIndex.Clear();
        for (var i = 0; i < xLabels.Count; i++)
        {
            xIndex[xLabels[i]] = i;
        }

        var cells = Enumerable.Range(0, yLabels.Count)
            .Select(_ => new double?[xLabels.Count])
            .ToArray();

        string?[][]? tooltips = tooltipColumn is null
            ? null
            : Enumerable.Range(0, yLabels.Count)
                .Select(_ => new string?[xLabels.Count])
                .ToArray();

        double min = double.PositiveInfinity;
        double max = double.NegativeInfinity;

        foreach (var row in rows)
        {
            var x = FormatHeatmapLabel(row.GetValueOrDefault(xColumn));
            var y = FormatHeatmapAxisLabel(row.GetValueOrDefault(yColumn), yFormat);
            if (string.IsNullOrEmpty(x) || string.IsNullOrEmpty(y))
            {
                continue;
            }

            if (!xIndex.TryGetValue(x, out var xi) || !yIndex.TryGetValue(y, out var yi))
            {
                continue;
            }

            var value = ToDouble(row.GetValueOrDefault(valueColumn));
            if (value is null)
            {
                continue;
            }

            string? tip = null;
            if (tooltips is not null && tooltipColumn is not null)
            {
                tip = FormatHeatmapLabel(row.GetValueOrDefault(tooltipColumn));
                if (string.IsNullOrWhiteSpace(tip))
                {
                    tip = null;
                }
            }

            var existing = cells[yi][xi];
            if (existing is not null)
            {
                if (value.Value < existing.Value)
                {
                    if (tooltips is not null && tip is not null && string.IsNullOrWhiteSpace(tooltips[yi][xi]))
                    {
                        tooltips[yi][xi] = tip;
                    }

                    continue;
                }

                if (value.Value == existing.Value && tooltips is not null && tip is not null)
                {
                    tooltips[yi][xi] = MergeTooltipStrings(tooltips[yi][xi], tip, tooltipSplit);
                    continue;
                }
            }

            cells[yi][xi] = value;
            if (tooltips is not null)
            {
                tooltips[yi][xi] = tip ?? tooltips[yi][xi];
            }

            min = Math.Min(min, value.Value);
            max = Math.Max(max, value.Value);
        }

        if (double.IsPositiveInfinity(min))
        {
            min = 0;
            max = 0;
        }

        return new MatrixPayload(xLabels, yLabels, cells, min, max, tooltips);
    }

    private static void SortHeatmapXLabels(List<string> xLabels)
    {
        xLabels.Sort((a, b) =>
        {
            var aDate = TryParseHeatmapDate(a);
            var bDate = TryParseHeatmapDate(b);
            if (aDate.HasValue && bDate.HasValue)
            {
                return aDate.Value.CompareTo(bDate.Value);
            }

            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static DateOnly? TryParseHeatmapDate(string label) =>
        DateOnly.TryParse(label, out var date) ? date : null;

    private static string FormatHeatmapLabel(object? value) =>
        value switch
        {
            null => string.Empty,
            DateTime dt => dt.ToString("yyyy-MM-dd"),
            DateOnly d => d.ToString("yyyy-MM-dd"),
            _ => Convert.ToString(value) ?? string.Empty,
        };

    private static string FormatHeatmapAxisLabel(object? value, string? format)
    {
        var raw = FormatHeatmapLabel(value);
        return string.IsNullOrEmpty(raw) ? raw : LabelFormat.Format(raw, format);
    }

    private static string MergeTooltipStrings(string? left, string right, string split)
    {
        var items = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in (left ?? string.Empty).Split(split, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            items.Add(part);
        }

        foreach (var part in right.Split(split, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            items.Add(part);
        }

        return string.Join(split, items.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
    }

    private static double? ToDouble(object? value) =>
        value switch
        {
            null => null,
            double d => d,
            float f => f,
            decimal m => (double)m,
            int i => i,
            long l => l,
            _ => double.TryParse(Convert.ToString(value), out var parsed) ? parsed : null,
        };

    private static string FormatValue(object? value) =>
        value switch
        {
            null => string.Empty,
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm"),
            DateOnly d => d.ToString("yyyy-MM-dd"),
            _ => Convert.ToString(value) ?? string.Empty,
        };
}

public sealed record ChartPayload(IReadOnlyList<string> Labels, IReadOnlyList<ChartSeries> Series);

public sealed record ChartSeries(string Name, IReadOnlyList<double?> Values);

public sealed record TablePayload(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);

public sealed record MatrixPayload(
    IReadOnlyList<string> XLabels,
    IReadOnlyList<string> YLabels,
    double?[][] Cells,
    double Min,
    double Max,
    string?[][]? Tooltips = null);
