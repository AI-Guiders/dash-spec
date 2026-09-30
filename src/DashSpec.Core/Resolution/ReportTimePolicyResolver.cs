using DashSpec.Core.Model;

namespace DashSpec.Core.Resolution;

public static class ReportTimePolicyResolver
{
    public static ReportTimePolicy ResolveEffective(
        ReportTimePolicy? specPolicy,
        IReadOnlyDictionary<string, string>? hostReportTimeSettings,
        string? fallbackTimeZoneId = null)
    {
        var merged = hostReportTimeSettings is { Count: > 0 }
            ? ReportTimePolicyParser.ParsePartial(hostReportTimeSettings, specPolicy)
              ?? specPolicy
              ?? ReportTimePolicy.Default
            : specPolicy ?? ReportTimePolicy.Default;
        if (merged.Basis is not ReportTimeBasis.Working)
        {
            return merged;
        }

        var calendar = merged.WorkCalendar ?? new WorkCalendarDefinition(null, null, null, null);
        calendar = calendar with
        {
            TimeZoneId = calendar.TimeZoneId ?? fallbackTimeZoneId,
            StartTime = calendar.StartTime ?? "09:00",
            EndTime = calendar.EndTime ?? "18:00",
            WorkDays = calendar.WorkDays ?? "mon,tue,wed,thu,fri",
        };

        return merged with { WorkCalendar = calendar };
    }
}
