using Microsoft.FSharp.Core;

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
        if (!merged.Basis.IsWorking)
        {
            return merged;
        }

        var calendar = merged.WorkCalendar?.Value;
        var resolvedCalendar = new WorkCalendarDefinition
        {
            TimeZoneId = ToOption(calendar?.TimeZoneId?.Value ?? fallbackTimeZoneId),
            StartTime = ToOption(calendar?.StartTime?.Value ?? "09:00"),
            EndTime = ToOption(calendar?.EndTime?.Value ?? "18:00"),
            WorkDays = ToOption(calendar?.WorkDays?.Value ?? "mon,tue,wed,thu,fri"),
        };

        return new ReportTimePolicy
        {
            Basis = merged.Basis,
            Apply = merged.Apply,
            WorkTimeColumn = merged.WorkTimeColumn,
            WorkCalendar = FSharpOption<WorkCalendarDefinition>.Some(resolvedCalendar),
        };
    }

    private static FSharpOption<string> ToOption(string? value) =>
        value is null ? FSharpOption<string>.None : FSharpOption<string>.Some(value);
}
