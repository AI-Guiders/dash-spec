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
                tooltip,
                diagram);
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

        return FinalizeMatrix(xLabels, yLabels, cells, min, max, tooltips, diagram);
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
        TooltipDefinition? tooltip,
        DiagramDefinition diagram)
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

        return FinalizeMatrix(xLabels, yLabels, cells, min, max, tooltips, diagram);
    }

    private static MatrixPayload FinalizeMatrix(
        IReadOnlyList<string> xLabels,
        IReadOnlyList<string> yLabels,
        double?[][] cells,
        double min,
        double max,
        string?[][]? tooltips,
        DiagramDefinition diagram)
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
            colMaxs);
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
