using DashSpec.Core.Model;

namespace DashSpec.Core.Runtime;

internal static class BoxPlotPayloadBuilder
{
    public static ChartPayload Build(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        DiagramDefinition diagram)
    {
        var valueColumn = DiagramBindings.TryGetColumn(diagram, "value", out var value)
            ? value
            : DiagramBindings.Column(diagram, "y");
        var hasCategory = DiagramBindings.TryGetColumn(diagram, "x", out var categoryColumn);

        var groups = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (!MeasureValues.TryReadDouble(row.GetValueOrDefault(valueColumn), out var sample))
            {
                continue;
            }

            var key = "all";
            if (hasCategory)
            {
                var raw = row.GetValueOrDefault(categoryColumn);
                key = raw is null or DBNull
                    ? "(null)"
                    : Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture) ?? "(null)";
            }

            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }

            list.Add(sample);
        }

        if (groups.Count == 0)
        {
            return new ChartPayload([], []);
        }

        var boxes = groups
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new BoxPlotGroup(g.Key, g.Value))
            .ToList();

        var labels = boxes.Select(b => b.Label).ToList();
        var seriesLabel = DiagramBindings.Label(diagram, "value")
            ?? DiagramBindings.Label(diagram, "y")
            ?? valueColumn;

        return new ChartPayload(
            labels,
            [new ChartSeries(seriesLabel, [])],
            Boxes: boxes);
    }
}
