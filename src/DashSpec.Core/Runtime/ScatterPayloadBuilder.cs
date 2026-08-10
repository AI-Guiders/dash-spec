using DashSpec.Core.Model;

namespace DashSpec.Core.Runtime;

internal static class ScatterPayloadBuilder
{
    public static ChartPayload Build(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        DiagramDefinition diagram)
    {
        var xColumn = DiagramBindings.Column(diagram, "x");
        var yColumn = DiagramBindings.Column(diagram, "y");
        var hasSize = DiagramBindings.TryGetColumn(diagram, "size", out var sizeColumn);
        var points = new List<ChartPoint>();

        foreach (var row in rows)
        {
            if (!MeasureValues.TryReadDouble(row.GetValueOrDefault(xColumn), out var x) ||
                !MeasureValues.TryReadDouble(row.GetValueOrDefault(yColumn), out var y))
            {
                continue;
            }

            double? size = null;
            if (hasSize &&
                MeasureValues.TryReadDouble(row.GetValueOrDefault(sizeColumn), out var sizeValue))
            {
                size = sizeValue;
            }

            points.Add(new ChartPoint(x, y, size));
        }

        var label = DiagramBindings.Label(diagram, "y") ?? yColumn;
        return new ChartPayload(
            Labels: [],
            Series: [new ChartSeries(label, [])],
            Points: points);
    }
}
