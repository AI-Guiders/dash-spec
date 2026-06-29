using DashSpec.Core.Model;

namespace DashSpec.Core.Runtime;

internal static class MatrixPayloadBuilder
{
    public static MatrixPayload Build(
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
            var x = PayloadRowFormatters.FormatHeatmapLabel(row.GetValueOrDefault(xColumn));
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
            var x = PayloadRowFormatters.FormatHeatmapLabel(row.GetValueOrDefault(xColumn));
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

            string? tip = null;
            if (tooltips is not null && tooltipColumn is not null)
            {
                tip = PayloadRowFormatters.FormatHeatmapLabel(row.GetValueOrDefault(tooltipColumn));
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
                    tooltips[yi][xi] = PayloadRowFormatters.MergeTooltipStrings(tooltips[yi][xi], tip, tooltipSplit);
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
            var aDate = PayloadRowFormatters.TryParseHeatmapDate(a);
            var bDate = PayloadRowFormatters.TryParseHeatmapDate(b);
            if (aDate.HasValue && bDate.HasValue)
            {
                return aDate.Value.CompareTo(bDate.Value);
            }

            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        });
    }
}
