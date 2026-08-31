#nullable enable
using System.Globalization;
using System.Text.RegularExpressions;
using DashSpec.Core.Runtime;

namespace DashSpec.Host.Commands;

/// <summary>Host preset table for /select date (DASHSPEC-ADR-0043 §3, ADR-0045 grains).</summary>
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
            error = "Укажите дату (today, YYYY-Www, YYYY-MM, YYYY-Q1, from..to).";
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

        var weekOnly = WeekOnlyPattern().Match(token);
        if (weekOnly.Success)
        {
            return TryResolveWeek(
                todayUtc.Year,
                int.Parse(weekOnly.Groups["week"].Value),
                out range,
                out error);
        }

        var weekMatch = WeekTokenPattern().Match(token);
        if (weekMatch.Success)
        {
            return TryResolveWeek(
                int.Parse(weekMatch.Groups["year"].Value),
                int.Parse(weekMatch.Groups["week"].Value),
                out range,
                out error);
        }

        var quarterOnly = QuarterOnlyPattern().Match(token);
        if (quarterOnly.Success)
        {
            return TryResolveQuarter(todayUtc.Year, int.Parse(quarterOnly.Groups["quarter"].Value), out range, out error);
        }

        var quarterMatch = QuarterTokenPattern().Match(token);
        if (quarterMatch.Success)
        {
            return TryResolveQuarter(
                int.Parse(quarterMatch.Groups["year"].Value),
                int.Parse(quarterMatch.Groups["quarter"].Value),
                out range,
                out error);
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

        error = $"Unknown date preset '{token}'. Use today, YYYY-Www, Www, YYYY-MM, YYYY-Q1, Q1, or from..to.";
        return false;
    }

    static bool TryResolveWeek(int year, int week, out DateRangeValue range, out string? error)
    {
        range = default;
        error = null;
        var weeksInYear = ISOWeek.GetWeeksInYear(year);
        if (week is < 1 or > 53 || week > weeksInYear)
        {
            error = $"Week must be between 1 and {weeksInYear}.";
            return false;
        }

        var first = DateOnly.FromDateTime(ISOWeek.ToDateTime(year, week, DayOfWeek.Monday));
        range = new DateRangeValue(first, first.AddDays(6));
        return true;
    }

    static bool TryResolveQuarter(int year, int quarter, out DateRangeValue range, out string? error)
    {
        range = default;
        error = null;
        if (quarter is < 1 or > 4)
        {
            error = "Quarter must be between 1 and 4.";
            return false;
        }

        var firstMonth = (quarter - 1) * 3 + 1;
        var first = new DateOnly(year, firstMonth, 1);
        var last = first.AddMonths(3).AddDays(-1);
        range = new DateRangeValue(first, last);
        return true;
    }

    [GeneratedRegex(@"^(?<year>\d{4})-(?<month>\d{2})$")]
    private static partial Regex MonthTokenPattern();

    [GeneratedRegex(@"^(?<year>\d{4})-W(?<week>\d{1,2})$", RegexOptions.IgnoreCase)]
    private static partial Regex WeekTokenPattern();

    [GeneratedRegex(@"^W(?<week>\d{1,2})$", RegexOptions.IgnoreCase)]
    private static partial Regex WeekOnlyPattern();

    [GeneratedRegex(@"^(?<year>\d{4})-Q(?<quarter>[1-4])$", RegexOptions.IgnoreCase)]
    private static partial Regex QuarterTokenPattern();

    [GeneratedRegex(@"^Q(?<quarter>[1-4])$", RegexOptions.IgnoreCase)]
    private static partial Regex QuarterOnlyPattern();
}
