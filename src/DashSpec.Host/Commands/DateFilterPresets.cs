#nullable enable
using System.Text.RegularExpressions;
using DashSpec.Core.Runtime;

namespace DashSpec.Host.Commands;

/// <summary>Host preset table for /select date (DASHSPEC-ADR-0043 §3).</summary>
internal static partial class DateFilterPresets
{
    public static bool TryResolve(
        string expression,
        DateOnly todayUtc,
        out DateRangeValue range,
        out string? error)
    {
        range = default;
        error = null;
        var token = expression.Trim();
        if (token.Length == 0)
        {
            error = "Date argument is required.";
            return false;
        }

        if (token.Equals("today", StringComparison.OrdinalIgnoreCase))
        {
            range = new DateRangeValue(todayUtc, todayUtc);
            return true;
        }

        if (token.Equals("last-week", StringComparison.OrdinalIgnoreCase))
        {
            range = new DateRangeValue(todayUtc.AddDays(-7), todayUtc);
            return true;
        }

        if (token.Equals("last-month", StringComparison.OrdinalIgnoreCase))
        {
            var firstOfMonth = new DateOnly(todayUtc.Year, todayUtc.Month, 1);
            var first = firstOfMonth.AddMonths(-1);
            var last = firstOfMonth.AddDays(-1);
            range = new DateRangeValue(first, last);
            return true;
        }

        var monthMatch = MonthTokenPattern().Match(token);
        if (monthMatch.Success)
        {
            var year = int.Parse(monthMatch.Groups["year"].Value);
            var month = int.Parse(monthMatch.Groups["month"].Value);
            var first = new DateOnly(year, month, 1);
            var last = first.AddMonths(1).AddDays(-1);
            range = new DateRangeValue(first, last);
            return true;
        }

        if (token.Contains("..", StringComparison.Ordinal))
        {
            try
            {
                range = DateDefaultRange.Resolve(token, todayUtc);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        error = $"Unknown date preset '{token}'. Use today, last-week, last-month, YYYY-MM, or from..to.";
        return false;
    }

    [GeneratedRegex(@"^(?<year>\d{4})-(?<month>\d{2})$")]
    private static partial Regex MonthTokenPattern();
}
