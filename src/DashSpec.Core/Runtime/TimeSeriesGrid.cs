using System.Globalization;
using System.Text.RegularExpressions;

namespace DashSpec.Core.Runtime;

internal static partial class TimeSeriesGrid
{
    // Spec: diagram x_step = "5m" | "15m" | "1h" … (number + unit), not a fixed enum in Core.
    [GeneratedRegex(@"^(\d+)\s*(m|min|h|hr|hour|hours|s|sec)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StepPattern();

    public static bool TryParseStep(string? raw, out TimeSpan step)
    {
        step = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var match = StepPattern().Match(raw.Trim());
        if (!match.Success)
        {
            return false;
        }

        if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var amount)
            || amount <= 0)
        {
            return false;
        }

        step = match.Groups[2].Value.ToLowerInvariant() switch
        {
            "m" or "min" => TimeSpan.FromMinutes(amount),
            "h" or "hr" or "hour" or "hours" => TimeSpan.FromHours(amount),
            "s" or "sec" => TimeSpan.FromSeconds(amount),
            _ => default,
        };

        return step > TimeSpan.Zero;
    }

    public static DateTime? TryParseBucket(object? value) =>
        value switch
        {
            null => null,
            DateTime dt => dt,
            DateOnly d => d.ToDateTime(TimeOnly.MinValue),
            _ => DateTime.TryParse(Convert.ToString(value), out var parsed) ? parsed : null,
        };

    public static DateTime Floor(DateTime value, TimeSpan step)
    {
        if (step <= TimeSpan.Zero)
        {
            return value;
        }

        var trimmed = new DateTime(
            value.Year,
            value.Month,
            value.Day,
            value.Hour,
            value.Minute,
            value.Second,
            value.Kind);

        var anchor = trimmed.Date;
        var elapsed = trimmed - anchor;
        var bucketIndex = elapsed.Ticks / step.Ticks;
        return anchor.AddTicks(bucketIndex * step.Ticks);
    }

    public static IEnumerable<DateTime> Range(DateTime from, DateTime to, TimeSpan step)
    {
        for (var current = from; current <= to; current = current.Add(step))
        {
            yield return current;
        }
    }
}
