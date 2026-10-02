using DashSpec.Abstractions.Data;
using System.Globalization;

using DashSpec.Core.Model;
using DashSpec.Core.Runtime;

namespace DashSpec.Execution.Runtime;

internal static class MatrixPayloadBuilder
{
    private const string DefaultTooltipMergeSplit = ", ";

    public static MatrixPayload Build(
        TypedRowBatch rows,
        DiagramDefinition diagram,
        SeriesTransformSettings? seriesTransform = null,
        TooltipDefinition? tooltip = null)
    {
        var xColumn = DiagramBindings.Column(diagram, "x");
        var yColumn = DiagramBindings.Column(diagram, "y");
        var valueColumn = DiagramBindings.Column(diagram, "value");
        diagram.Properties.TryGetValue("x_format", out var xFormatRaw);
        var xFormat = LabelFormat.ResolveAxisFormat(xFormatRaw);
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
                tooltip,
                diagram);
        }

        var xLabels = new List<string>();
        var xIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var xSortKeys = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        var yLabels = new List<string>();
        var yIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var yTotals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows.Rows)
        {
            var rawX = row.GetClr(xColumn);
            var x = PayloadRowFormatters.FormatHeatmapAxisLabel(rawX, xFormat);
            var y = PayloadRowFormatters.FormatHeatmapAxisLabel(row.GetClr(yColumn), yFormat);
            if (string.IsNullOrEmpty(x) || string.IsNullOrEmpty(y))
            {
                continue;
            }

            if (!xIndex.ContainsKey(x))
            {
                xIndex[x] = xLabels.Count;
                xLabels.Add(x);
                xSortKeys[x] = AxisLabelSort.ResolveSortKey(rawX, xFormat);
            }
            else
            {
                var sortKey = AxisLabelSort.ResolveSortKey(rawX, xFormat);
                if (sortKey < xSortKeys[x])
                {
                    xSortKeys[x] = sortKey;
                }
            }

            if (!yIndex.ContainsKey(y))
            {
                yIndex[y] = yLabels.Count;
                yLabels.Add(y);
            }

            var value = PayloadRowFormatters.ToDouble(row.GetClr(valueColumn)) ?? 0;
            yTotals[y] = yTotals.GetValueOrDefault(y) + value;
        }

        CategoryAxisOrdering.Sort(yLabels, yTotals, diagram.Properties, CategoryAxis.Y);
        yIndex.Clear();
        for (var i = 0; i < yLabels.Count; i++)
        {
            yIndex[yLabels[i]] = i;
        }

        xLabels.Sort((a, b) => xSortKeys[a].CompareTo(xSortKeys[b]));

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

        foreach (var row in rows.Rows)
        {
            var x = PayloadRowFormatters.FormatHeatmapAxisLabel(row.GetClr(xColumn), xFormat);
            var y = PayloadRowFormatters.FormatHeatmapAxisLabel(row.GetClr(yColumn), yFormat);
            if (string.IsNullOrEmpty(x) || string.IsNullOrEmpty(y))
            {
                continue;
            }

            if (!xIndex.TryGetValue(x, out var xi) || !yIndex.TryGetValue(y, out var yi))
            {
                continue;
            }

            var value = PayloadRowFormatters.ToDouble(row.GetClr(valueColumn));
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

        return FinalizeMatrix(xLabels, yLabels, cells, min, max, tooltips, diagram, xBucketStarts: null);
    }

    private static MatrixPayload BuildHourGrid(
        TypedRowBatch rows,
        string xColumn,
        string yColumn,
        string valueColumn,
        string? xFormat,
        string? yFormat,
        TimeSpan xStep,
        SeriesTransformSettings? seriesTransform,
        TooltipDefinition? tooltip,
        DiagramDefinition diagram)
    {
        var buckets = new SortedDictionary<DateTime, Dictionary<string, double?>>(Comparer<DateTime>.Default);
        var bucketRows = new SortedDictionary<DateTime, Dictionary<string, TypedDataRow>>(
            Comparer<DateTime>.Default);
        var yLabels = new List<string>();
        var yIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var yTotals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows.Rows)
        {
            var bucket = TimeSeriesGrid.TryParseBucket(row.GetClr(xColumn));
            if (bucket is null)
            {
                continue;
            }

            var xKey = TimeSeriesGrid.Floor(bucket.Value, xStep);
            var y = PayloadRowFormatters.FormatHeatmapAxisLabel(row.GetClr(yColumn), yFormat);
            if (string.IsNullOrEmpty(y))
            {
                continue;
            }

            if (!buckets.TryGetValue(xKey, out var seriesValues))
            {
                seriesValues = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);
                buckets[xKey] = seriesValues;
                bucketRows[xKey] = new Dictionary<string, TypedDataRow>(StringComparer.OrdinalIgnoreCase);
            }

            if (!yIndex.ContainsKey(y))
            {
                yIndex[y] = yLabels.Count;
                yLabels.Add(y);
            }

            var value = PayloadRowFormatters.ToDouble(row.GetClr(valueColumn)) ?? 0;
            if (seriesValues.TryGetValue(y, out var existing) && existing is not null)
            {
                seriesValues[y] = Math.Max(existing.Value, value);
            }
            else
            {
                seriesValues[y] = value;
            }
            bucketRows[xKey][y] = row;
            yTotals[y] = yTotals.GetValueOrDefault(y) + value;
        }

        CategoryAxisOrdering.Sort(yLabels, yTotals, diagram.Properties, CategoryAxis.Y);
        yIndex.Clear();
        for (var i = 0; i < yLabels.Count; i++)
        {
            yIndex[yLabels[i]] = i;
        }

        List<DateTime> xKeys;
        List<string> xLabels;
        var axisWindow = TryParseAxisWindow(diagram);
        if (buckets.Count > 0 && LabelFormat.DisplayTimeZone is { } displayTz)
        {
            (xKeys, xLabels) = BuildDisplayLocalTimeAxis(
                buckets.Keys.Min(),
                displayTz,
                xStep,
                axisWindow);
        }
        else if (buckets.Count > 0)
        {
            var day = buckets.Keys.First().Date;
            var (rangeStart, rangeEnd) = ResolveUtcStorageWindow(day, axisWindow);
            var expanded = new SortedDictionary<DateTime, Dictionary<string, double?>>(Comparer<DateTime>.Default);
            var expandedRows = new SortedDictionary<DateTime, Dictionary<string, TypedDataRow>>(
                Comparer<DateTime>.Default);
            for (var slot = rangeStart; slot < rangeEnd; slot = slot.Add(xStep))
            {
                expanded[slot] = buckets.TryGetValue(slot, out var values)
                    ? new Dictionary<string, double?>(values, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);
                expandedRows[slot] = bucketRows.TryGetValue(slot, out var rowMap)
                    ? new Dictionary<string, TypedDataRow>(rowMap, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, TypedDataRow>(StringComparer.OrdinalIgnoreCase);
            }

            buckets = expanded;
            bucketRows = expandedRows;
            xKeys = buckets.Keys.ToList();
            xLabels = xKeys
                .Select(key => PayloadRowFormatters.FormatChartAxisLabel(key, xFormat))
                .ToList();
        }
        else
        {
            xKeys = [];
            xLabels = [];
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

        for (var xi = 0; xi < xKeys.Count; xi++)
        {
            var xKey = xKeys[xi];
            if (!buckets.TryGetValue(xKey, out var seriesValues))
            {
                seriesValues = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);
            }

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
        }

        if (double.IsPositiveInfinity(min))
        {
            min = 0;
            max = 0;
        }

        return FinalizeMatrix(xLabels, yLabels, cells, min, max, tooltips, diagram, xBucketStarts: xKeys);
    }

    private readonly record struct AxisWindow(TimeOnly From, TimeOnly To);

    private static AxisWindow? TryParseAxisWindow(DiagramDefinition diagram)
    {
        if (!diagram.Properties.TryGetValue("axis_from", out var fromRaw)
            || !diagram.Properties.TryGetValue("axis_to", out var toRaw)
            || !TimeOnly.TryParse(fromRaw.Trim(), CultureInfo.InvariantCulture, out var from)
            || !TimeOnly.TryParse(toRaw.Trim(), CultureInfo.InvariantCulture, out var to)
            || to <= from)
        {
            return null;
        }

        return new AxisWindow(from, to);
    }

    private static (DateTime RangeStart, DateTime RangeEnd) ResolveUtcStorageWindow(DateTime anchorDay, AxisWindow? window)
    {
        if (window is null)
        {
            return (anchorDay, anchorDay.AddDays(1));
        }

        var start = anchorDay.Date.Add(window.Value.From.ToTimeSpan());
        var end = anchorDay.Date.Add(window.Value.To.ToTimeSpan());
        return end > start ? (start, end) : (start, start.AddHours(1));
    }

    private static (List<DateTime> UtcKeys, List<string> Labels) BuildDisplayLocalTimeAxis(
        DateTime anchorUtcBucket,
        TimeZoneInfo displayTz,
        TimeSpan xStep,
        AxisWindow? window)
    {
        var anchorUtc = DateValueCodec.NormalizeStorageUtc(anchorUtcBucket);
        var anchorLocalDate = TimeZoneInfo.ConvertTimeFromUtc(anchorUtc, displayTz).Date;
        DateTime localStart;
        DateTime localEnd;
        if (window is { } w)
        {
            localStart = DateTime.SpecifyKind(anchorLocalDate.Add(w.From.ToTimeSpan()), DateTimeKind.Unspecified);
            localEnd = DateTime.SpecifyKind(anchorLocalDate.Add(w.To.ToTimeSpan()), DateTimeKind.Unspecified);
        }
        else
        {
            localStart = DateTime.SpecifyKind(anchorLocalDate, DateTimeKind.Unspecified);
            localEnd = localStart.AddDays(1);
        }

        if (localEnd <= localStart)
        {
            localEnd = localStart.Add(xStep);
        }

        var utcKeys = new List<DateTime>();
        var labels = new List<string>();
        for (var local = localStart; local < localEnd; local = local.Add(xStep))
        {
            var utc = TimeZoneInfo.ConvertTimeToUtc(local, displayTz);
            var key = TimeSeriesGrid.Floor(utc, xStep);
            utcKeys.Add(key);
            labels.Add(TimeOnly.FromDateTime(local).ToString("HH:mm", CultureInfo.InvariantCulture));
        }

        return (utcKeys, labels);
    }

    private static MatrixPayload FinalizeMatrix(
        IReadOnlyList<string> xLabels,
        IReadOnlyList<string> yLabels,
        double?[][] cells,
        double min,
        double max,
        string?[][]? tooltips,
        DiagramDefinition diagram,
        IReadOnlyList<DateTime>? xBucketStarts)
    {
        diagram.Properties.TryGetValue("color_normalize", out var normalizeRaw);
        var normalize = MatrixColorNormalizeParser.Parse(normalizeRaw);

        double[]? rowMins = null;
        double[]? rowMaxs = null;
        double[]? colMins = null;
        double[]? colMaxs = null;

        switch (normalize)
        {
            case MatrixColorNormalize.Row:
                (rowMins, rowMaxs) = ComputeRowRanges(cells, min, max);
                break;
            case MatrixColorNormalize.Column:
                (colMins, colMaxs) = ComputeColumnRanges(cells, min, max);
                break;
        }

        return new MatrixPayload(
            xLabels,
            yLabels,
            cells,
            min,
            max,
            tooltips,
            normalize,
            rowMins,
            rowMaxs,
            colMins,
            colMaxs,
            xBucketStarts);
    }

    private static (double[] RowMins, double[] RowMaxs) ComputeRowRanges(
        double?[][] cells,
        double fallbackMin,
        double fallbackMax)
    {
        var rowMins = new double[cells.Length];
        var rowMaxs = new double[cells.Length];
        for (var yi = 0; yi < cells.Length; yi++)
        {
            var rMin = double.PositiveInfinity;
            var rMax = double.NegativeInfinity;
            foreach (var cell in cells[yi])
            {
                if (cell is null)
                {
                    continue;
                }

                rMin = Math.Min(rMin, cell.Value);
                rMax = Math.Max(rMax, cell.Value);
            }

            if (double.IsPositiveInfinity(rMin))
            {
                rowMins[yi] = fallbackMin;
                rowMaxs[yi] = fallbackMax;
            }
            else
            {
                rowMins[yi] = rMin;
                rowMaxs[yi] = rMax;
            }
        }

        return (rowMins, rowMaxs);
    }

    private static (double[] ColMins, double[] ColMaxs) ComputeColumnRanges(
        double?[][] cells,
        double fallbackMin,
        double fallbackMax)
    {
        var colCount = cells.Length == 0 ? 0 : cells[0].Length;
        var colMins = new double[colCount];
        var colMaxs = new double[colCount];
        for (var xi = 0; xi < colCount; xi++)
        {
            var cMin = double.PositiveInfinity;
            var cMax = double.NegativeInfinity;
            foreach (var row in cells)
            {
                if ((uint)xi >= (uint)row.Length || row[xi] is not { } value)
                {
                    continue;
                }

                cMin = Math.Min(cMin, value);
                cMax = Math.Max(cMax, value);
            }

            if (double.IsPositiveInfinity(cMin))
            {
                colMins[xi] = fallbackMin;
                colMaxs[xi] = fallbackMax;
            }
            else
            {
                colMins[xi] = cMin;
                colMaxs[xi] = cMax;
            }
        }

        return (colMins, colMaxs);
    }

}
