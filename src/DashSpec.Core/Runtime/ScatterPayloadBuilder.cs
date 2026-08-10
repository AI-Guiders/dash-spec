using System.Globalization;
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
        var points = new List<ChartPoint>();

        foreach (var row in rows)
        {
            if (!TryReadDouble(row.GetValueOrDefault(xColumn), out var x) ||
                !TryReadDouble(row.GetValueOrDefault(yColumn), out var y))
            {
                continue;
            }

            points.Add(new ChartPoint(x, y));
        }

        var label = DiagramBindings.Label(diagram, "y") ?? yColumn;
        return new ChartPayload(
            Labels: [],
            Series: [new ChartSeries(label, [])],
            Points: points);
    }

    private static bool TryReadDouble(object? value, out double number)
    {
        number = 0;
        return value switch
        {
            null or DBNull => false,
            double d => Accept(d, out number),
            float f => Accept(f, out number),
            decimal m => Accept((double)m, out number),
            byte or sbyte or short or ushort or int or uint or long or ulong =>
                Accept(Convert.ToDouble(value, CultureInfo.InvariantCulture), out number),
            string s when double.TryParse(
                s,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed) => Accept(parsed, out number),
            IConvertible c => Accept(Convert.ToDouble(c, CultureInfo.InvariantCulture), out number),
            _ => false,
        };

        static bool Accept(double d, out double n)
        {
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                n = 0;
                return false;
            }

            n = d;
            return true;
        }
    }
}
