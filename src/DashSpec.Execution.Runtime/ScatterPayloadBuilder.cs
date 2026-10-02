using DashSpec.Abstractions.Data;
using DashSpec.Core.Model;

namespace DashSpec.Execution.Runtime;

internal static class ScatterPayloadBuilder
{
    public static ChartPayload Build(
        TypedRowBatch rows,
        DiagramDefinition diagram)
    {
        var xColumn = DiagramBindings.Column(diagram, "x");
        var yColumn = DiagramBindings.Column(diagram, "y");
        var hasSize = DiagramBindings.TryGetColumn(diagram, "size", out var sizeColumn);
        var points = new List<ChartPoint>();

        foreach (var row in rows.Rows)
        {
            if (!MeasureValues.TryReadDouble(row.GetClr(xColumn), out var x) ||
                !MeasureValues.TryReadDouble(row.GetClr(yColumn), out var y))
            {
                continue;
            }

            double? size = null;
            if (hasSize &&
                MeasureValues.TryReadDouble(row.GetClr(sizeColumn), out var sizeValue))
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
