namespace DashSpec.Core.Model;

public enum ReportTimeBasis
{
    Calendar,
    Working,
}

public enum ReportTimeApply
{
    Clip,
    Measure,
    Both,
}

public sealed record WorkCalendarDefinition(
    string? TimeZoneId,
    string? StartTime,
    string? EndTime,
    string? WorkDays);

public sealed record ReportTimePolicy(
    ReportTimeBasis Basis = ReportTimeBasis.Calendar,
    ReportTimeApply Apply = ReportTimeApply.Clip,
    string? WorkTimeColumn = null,
    WorkCalendarDefinition? WorkCalendar = null)
{
    public static ReportTimePolicy Default { get; } = new();

    public bool ClipsQueries =>
        Basis is ReportTimeBasis.Working
        && Apply is ReportTimeApply.Clip or ReportTimeApply.Both
        && !string.IsNullOrWhiteSpace(WorkTimeColumn);
}
