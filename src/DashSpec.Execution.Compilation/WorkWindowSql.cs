using System.Globalization;
using DashSpec.Core.Model;

namespace DashSpec.Execution.Compilation;

internal static class WorkWindowSql
{
    private static readonly Dictionary<string, int> WeekdayMask = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mon"] = 0,
        ["monday"] = 0,
        ["tue"] = 1,
        ["tuesday"] = 1,
        ["wed"] = 2,
        ["wednesday"] = 2,
        ["thu"] = 3,
        ["thursday"] = 3,
        ["fri"] = 4,
        ["friday"] = 4,
        ["sat"] = 5,
        ["saturday"] = 5,
        ["sun"] = 6,
        ["sunday"] = 6,
    };

    public static string? TryBuildWorkClipPredicate(ReportTimePolicy policy, string dialectId)
    {
        if (!policy.ClipsQueries || !string.Equals(dialectId, "tsql", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var column = policy.WorkTimeColumn!.Trim();
        var calendar = policy.WorkCalendar!;
        var windowsTz = ResolveSqlServerTimeZoneId(calendar.TimeZoneId);
        if (windowsTz is null)
        {
            return null;
        }

        if (!TryParseTime(calendar.StartTime ?? "09:00", out var start)
            || !TryParseTime(calendar.EndTime ?? "18:00", out var end))
        {
            return null;
        }

        var weekdaySet = ParseWeekdays(calendar.WorkDays ?? "mon,tue,wed,thu,fri");
        if (weekdaySet.Count == 0)
        {
            return null;
        }

        var localExpr =
            $"(({column} AT TIME ZONE 'UTC') AT TIME ZONE '{windowsTz.Replace("'", "''")}')";
        var weekdayExpr = $"((DATEDIFF(day, 0, CAST({localExpr} AS date)) + 6) % 7)";
        var weekdayList = string.Join(", ", weekdaySet.OrderBy(x => x));
        var startLiteral = start.ToString("HH\\:mm\\:ss", CultureInfo.InvariantCulture);
        var endLiteral = end.ToString("HH\\:mm\\:ss", CultureInfo.InvariantCulture);

        return
            $"({weekdayExpr} IN ({weekdayList}) AND CAST({localExpr} AS time) >= '{startLiteral}' AND CAST({localExpr} AS time) < '{endLiteral}')";
    }

    private static HashSet<int> ParseWeekdays(string raw)
    {
        var set = new HashSet<int>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (WeekdayMask.TryGetValue(part, out var index))
            {
                set.Add(index);
            }
        }

        return set;
    }

    private static bool TryParseTime(string raw, out TimeOnly time)
    {
        if (TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
        {
            return true;
        }

        return TimeOnly.TryParse(raw, out time);
    }

    private static string? ResolveSqlServerTimeZoneId(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        var trimmed = timeZoneId.Trim();
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(trimmed);
            return tz.Id;
        }
        catch (TimeZoneNotFoundException)
        {
            return trimmed;
        }
        catch (InvalidTimeZoneException)
        {
            return trimmed;
        }
    }
}
