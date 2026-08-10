using System.Globalization;
using DashSpec.Core.Model;

namespace DashSpec.Core.Runtime;

internal static class GaugePayloadBuilder
{
    public static ChartPayload Build(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        DiagramDefinition diagram)
    {
        if (rows.Count == 0 ||
            !DiagramBindings.TryGetColumn(diagram, "value", out var valueColumn) ||
            !MeasureValues.TryReadDouble(rows[0].GetValueOrDefault(valueColumn), out var value))
        {
            return new ChartPayload([], []);
        }

        var min = ReadBound(diagram, "min", 0);
        var max = ReadBound(diagram, "max", Math.Max(100, value));
        if (max <= min)
        {
            max = min + 1;
        }

        var clamped = Math.Clamp(value, min, max);
        var label = DiagramBindings.Label(diagram, "value") ?? valueColumn;
        var gauge = new GaugeReading(clamped, min, max, label);

        return new ChartPayload(
            [label],
            [new ChartSeries(label, [clamped])],
            Gauge: gauge);
    }

    private static double ReadBound(DiagramDefinition diagram, string key, double fallback)
    {
        if (!diagram.Properties.TryGetValue(key, out var raw) ||
            !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return fallback;
        }

        return parsed;
    }
}
