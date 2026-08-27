using DashSpec.Core.Model;

namespace DashSpec.Core.Runtime;

internal static class MatrixPayloadBuilder
{
    private const string DefaultTooltipMergeSplit = ", ";

    public static MatrixPayload Build(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        DiagramDefinition diagram,
        SeriesTransformSettings? seriesTransform = null,
        TooltipDefinition? tooltip = null)
    {
        var xColumn = DiagramBindings.Column(diagram, "x");
        var yColumn = DiagramBindings.Column(diagram, "y");
        var valueColumn = DiagramBindings.Column(diagram, "value");
        diagram.Properties.TryGetValue("x_format", out var xFormat);
        diagram.Properties.TryGetValue("y_format", out var yFormat);
        diagram.Properties.TryGetValue("x_step", out var xStepRaw);

        if (TimeSeriesGrid.TryParseStep(xStepRaw, out var xStep) &&
            string.Equals(xFormat, "time.short", StringComparison.OrdinalIgnoreCase))
        {
            return BuildHourGrid(
                rows,
                xColumn,
                yColumn,
                valueColumn,
                xFormat,
                yFormat,
                xStep,
                seriesTransform,
                tooltip);
        }

        var xLabels = new List<string>();
        var xIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var yLabels = new List<string>();
        var yIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var yTotals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var x = PayloadRowFormatters.FormatHeatmapAxisLabel(row.GetValueOrDefault(xColumn), xFormat);
            var y = PayloadRowFormatters.FormatHeatmapAxisLabel(row.GetValueOrDefault(yColumn), yFormat);
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

            var value = PayloadRowFormatters.ToDouble(row.GetValueOrDefault(valueColumn)) ?? 0;
            yTotals[y] = yTotals.GetValueOrDefault(y) + value;
        }

        yLabels.Sort((a, b) => yTotals.GetValueOrDefault(b).CompareTo(yTotals.GetValueOrDefault(a)));
        TruncateYLabels(yLabels, seriesTransform);
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

        string?[][]? tooltips = tooltip is null
            ? null
            : Enumerable.Range(0, yLabels.Count)
                .Select(_ => new string?[xLabels.Count])
                .ToArray();

        double min = double.PositiveInfinity;
        double max = double.NegativeInfinity;

        foreach (var row in rows)
        {
            var x = PayloadRowFormatters.FormatHeatmapAxisLabel(row.GetValueOrDefault(xColumn), xFormat);
            var y = PayloadRowFormatters.FormatHeatmapAxisLabel(row.GetValueOrDefault(yColumn), yFormat);
            if (string.IsNullOrEmpty(x) || string.IsNullOrEmpty(y))
            {
                continue;
            }

            if (!xIndex.TryGetValue(x, out var xi) || !yIndex.TryGetValue(y, out var yi))
            {
                continue;
            }

            var value = PayloadRowFormatters.ToDouble(row.GetValueOrDefault(valueColumn));
            if (value is null)
            {
                continue;
            }

            string? tip = tooltip is null ? null : TooltipTemplate.Render(tooltip, row);

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
                    tooltips[yi][xi] = PayloadRowFormatters.MergeTooltipStrings(
                        tooltips[yi][xi],
                        tip,
                        DefaultTooltipMergeSplit);
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

    private static MatrixPayload BuildHourGrid(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        string xColumn,
        string yColumn,
        string valueColumn,
        string? xFormat,
        string? yFormat,
        TimeSpan xStep,
        SeriesTransformSettings? seriesTransform,
        TooltipDefinition? tooltip)
    {
        var buckets = new SortedDictionary<DateTime, Dictionary<string, double?>>(Comparer<DateTime>.Default);
        var bucketRows = new SortedDictionary<DateTime, Dictionary<string, IReadOnlyDictionary<string, object?>>>(
            Comparer<DateTime>.Default);
        var yLabels = new List<string>();
        var yIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var yTotals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var bucket = TimeSeriesGrid.TryParseBucket(row.GetValueOrDefault(xColumn));
            if (bucket is null)
            {
                continue;
            }

            var xKey = TimeSeriesGrid.Floor(bucket.Value, xStep);
            var y = PayloadRowFormatters.FormatHeatmapAxisLabel(row.GetValueOrDefault(yColumn), yFormat);
            if (string.IsNullOrEmpty(y))
            {
                continue;
            }

            if (!buckets.TryGetValue(xKey, out var seriesValues))
            {
                seriesValues = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);
                buckets[xKey] = seriesValues;
                bucketRows[xKey] = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
            }

            if (!yIndex.ContainsKey(y))
            {
                yIndex[y] = yLabels.Count;
                yLabels.Add(y);
            }

            var value = PayloadRowFormatters.ToDouble(row.GetValueOrDefault(valueColumn)) ?? 0;
            seriesValues[y] = value;
            bucketRows[xKey][y] = row;
            yTotals[y] = yTotals.GetValueOrDefault(y) + value;
        }

        yLabels.Sort((a, b) => yTotals.GetValueOrDefault(b).CompareTo(yTotals.GetValueOrDefault(a)));
        TruncateYLabels(yLabels, seriesTransform);
        yIndex.Clear();
        for (var i = 0; i < yLabels.Count; i++)
        {
            yIndex[yLabels[i]] = i;
        }

        if (buckets.Count > 0)
        {
            var day = buckets.Keys.First().Date;
            var expanded = new SortedDictionary<DateTime, Dictionary<string, double?>>(Comparer<DateTime>.Default);
            var expandedRows = new SortedDictionary<DateTime, Dictionary<string, IReadOnlyDictionary<string, object?>>>(
                Comparer<DateTime>.Default);
            for (var hour = day; hour < day.AddDays(1); hour = hour.Add(xStep))
            {
                expanded[hour] = buckets.TryGetValue(hour, out var values)
                    ? new Dictionary<string, double?>(values, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);
                expandedRows[hour] = bucketRows.TryGetValue(hour, out var rowMap)
                    ? new Dictionary<string, IReadOnlyDictionary<string, object?>>(rowMap, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
            }

            buckets = expanded;
            bucketRows = expandedRows;
        }

        var xLabels = buckets.Keys
            .Select(key => PayloadRowFormatters.FormatChartAxisLabel(key, xFormat))
            .ToList();

        var cells = Enumerable.Range(0, yLabels.Count)
            .Select(_ => new double?[xLabels.Count])
            .ToArray();

        string?[][]? tooltips = tooltip is null
            ? null
            : Enumerable.Range(0, yLabels.Count)
                .Select(_ => new string?[xLabels.Count])
                .ToArray();

        double min = double.PositiveInfinity;
        double max = double.NegativeInfinity;

        var xi = 0;
        foreach (var (xKey, seriesValues) in buckets)
        {
            foreach (var (y, value) in seriesValues)
            {
                if (!yIndex.TryGetValue(y, out var yi) || value is null)
                {
                    continue;
                }

                cells[yi][xi] = value;
                min = Math.Min(min, value.Value);
                max = Math.Max(max, value.Value);

                if (tooltips is not null &&
                    tooltip is not null &&
                    bucketRows.TryGetValue(xKey, out var rowMap) &&
                    rowMap.TryGetValue(y, out var row))
                {
                    tooltips[yi][xi] = TooltipTemplate.Render(tooltip, row);
                }
            }

            xi++;
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
            if (TimeOnly.TryParse(a, out var aTime) && TimeOnly.TryParse(b, out var bTime))
            {
                return aTime.CompareTo(bTime);
            }

            var aDate = PayloadRowFormatters.TryParseHeatmapDate(a);
            var bDate = PayloadRowFormatters.TryParseHeatmapDate(b);
            if (aDate.HasValue && bDate.HasValue)
            {
                return aDate.Value.CompareTo(bDate.Value);
            }

            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static void TruncateYLabels(List<string> yLabels, SeriesTransformSettings? seriesTransform)
    {
        if (seriesTransform is null || yLabels.Count <= seriesTransform.Max)
        {
            return;
        }

        yLabels.RemoveRange(seriesTransform.Max, yLabels.Count - seriesTransform.Max);
    }
}
