using System.Globalization;
using DashSpec.Core.Model;

namespace DashSpec.Core.Runtime;

internal static class HistogramPayloadBuilder
{
    public static ChartPayload Build(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        DiagramDefinition diagram)
    {
        var valueColumn = DiagramBindings.TryGetColumn(diagram, "value", out var value)
            ? value
            : DiagramBindings.Column(diagram, "x");

        var samples = new List<double>();
        foreach (var row in rows)
        {
            if (TryReadDouble(row.GetValueOrDefault(valueColumn), out var sample))
            {
                samples.Add(sample);
            }
        }

        if (samples.Count == 0)
        {
            return new ChartPayload([], []);
        }

        samples.Sort();
        var min = samples[0];
        var max = samples[^1];
        if (Math.Abs(max - min) < 1e-9)
        {
            max = min + 1;
        }

        var binCount = ResolveBinCount(diagram, samples.Count, min, max);
        var width = (max - min) / binCount;
        var counts = new double?[binCount];
        var labels = new string[binCount];
        for (var i = 0; i < binCount; i++)
        {
            counts[i] = 0;
            var lo = min + (i * width);
            var hi = i == binCount - 1 ? max : min + ((i + 1) * width);
            labels[i] = $"{FormatEdge(lo)}–{FormatEdge(hi)}";
        }

        foreach (var sample in samples)
        {
            var index = (int)Math.Floor((sample - min) / width);
            if (index < 0)
            {
                index = 0;
            }
            else if (index >= binCount)
            {
                index = binCount - 1;
            }

            counts[index] = (counts[index] ?? 0) + 1;
        }

        var seriesLabel = DiagramBindings.Label(diagram, "value")
            ?? DiagramBindings.Label(diagram, "x")
            ?? "count";
        return new ChartPayload(labels, [new ChartSeries(seriesLabel, counts)]);
    }

    private static int ResolveBinCount(DiagramDefinition diagram, int sampleCount, double min, double max)
    {
        if (diagram.Properties.TryGetValue("bins", out var rawBins) &&
            int.TryParse(rawBins, out var bins) &&
            bins is >= 2 and <= 100)
        {
            return bins;
        }

        if (diagram.Properties.TryGetValue("bin_width", out var rawWidth) &&
            double.TryParse(
                rawWidth,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var width) &&
            width > 0)
        {
            return Math.Clamp((int)Math.Ceiling((max - min) / width), 2, 100);
        }

        // Sturges-ish default, capped for dashboard chrome.
        return Math.Clamp((int)Math.Ceiling(Math.Log2(sampleCount)) + 1, 5, 20);
    }

    private static string FormatEdge(double value) =>
        Math.Abs(value) >= 100 || Math.Abs(value % 1) < 1e-6
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);

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
