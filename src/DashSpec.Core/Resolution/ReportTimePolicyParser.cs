using DashSpec.Core.Model;

namespace DashSpec.Core.Resolution;

public static class ReportTimePolicyParser
{
    private static readonly string[] ConfigurationKeys =
    [
        "time_basis",
        "time_apply",
        "work_time_column",
        "work_timezone",
        "work_start",
        "work_end",
        "work_days",
    ];

    public static bool IsConfigurationKey(string key) =>
        ConfigurationKeys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));

    public static ReportTimePolicy? ParseConfiguration(IReadOnlyDictionary<string, string> properties)
    {
        if (properties.Count == 0)
        {
            return null;
        }

        var hasAny = ConfigurationKeys.Any(k => properties.ContainsKey(k));
        if (!hasAny)
        {
            return null;
        }

        return ParsePartial(properties, null);
    }

    public static ReportTimePolicy Merge(ReportTimePolicy? basePolicy, ReportTimePolicy? overlay)
    {
        if (overlay is null)
        {
            return basePolicy ?? ReportTimePolicy.Default;
        }

        if (basePolicy is null)
        {
            return overlay;
        }

        var calendar = overlay.WorkCalendar ?? basePolicy.WorkCalendar;
        if (overlay.WorkCalendar is not null && basePolicy.WorkCalendar is not null)
        {
            calendar = new WorkCalendarDefinition(
                overlay.WorkCalendar.TimeZoneId ?? basePolicy.WorkCalendar.TimeZoneId,
                overlay.WorkCalendar.StartTime ?? basePolicy.WorkCalendar.StartTime,
                overlay.WorkCalendar.EndTime ?? basePolicy.WorkCalendar.EndTime,
                overlay.WorkCalendar.WorkDays ?? basePolicy.WorkCalendar.WorkDays);
        }

        return basePolicy with
        {
            Basis = overlay.Basis,
            Apply = overlay.Apply,
            WorkTimeColumn = overlay.WorkTimeColumn ?? basePolicy.WorkTimeColumn,
            WorkCalendar = calendar,
        };
    }

    public static ReportTimePolicy? ParsePartial(
        IReadOnlyDictionary<string, string> properties,
        ReportTimePolicy? existing)
    {
        var basis = existing?.Basis ?? ReportTimeBasis.Calendar;
        if (properties.TryGetValue("time_basis", out var basisRaw))
        {
            basis = ParseBasis(basisRaw);
        }

        var apply = existing?.Apply ?? ReportTimeApply.Clip;
        if (properties.TryGetValue("time_apply", out var applyRaw))
        {
            apply = ParseApply(applyRaw);
        }

        string? workColumn = existing?.WorkTimeColumn;
        if (properties.TryGetValue("work_time_column", out var columnRaw) && !string.IsNullOrWhiteSpace(columnRaw))
        {
            workColumn = columnRaw.Trim();
        }

        WorkCalendarDefinition? calendar = existing?.WorkCalendar;
        if (properties.TryGetValue("work_timezone", out var tz) && !string.IsNullOrWhiteSpace(tz)
            || properties.TryGetValue("work_start", out _)
            || properties.TryGetValue("work_end", out _)
            || properties.TryGetValue("work_days", out _))
        {
            calendar = new WorkCalendarDefinition(
                properties.TryGetValue("work_timezone", out var tzRaw) && !string.IsNullOrWhiteSpace(tzRaw)
                    ? tzRaw.Trim()
                    : calendar?.TimeZoneId,
                properties.TryGetValue("work_start", out var startRaw) && !string.IsNullOrWhiteSpace(startRaw)
                    ? startRaw.Trim()
                    : calendar?.StartTime,
                properties.TryGetValue("work_end", out var endRaw) && !string.IsNullOrWhiteSpace(endRaw)
                    ? endRaw.Trim()
                    : calendar?.EndTime,
                properties.TryGetValue("work_days", out var daysRaw) && !string.IsNullOrWhiteSpace(daysRaw)
                    ? daysRaw.Trim()
                    : calendar?.WorkDays);
        }

        return new ReportTimePolicy(basis, apply, workColumn, calendar);
    }

    public static ReportTimeBasis ParseBasis(string raw) =>
        raw.Trim().ToLowerInvariant() switch
        {
            "working" or "work" => ReportTimeBasis.Working,
            _ => ReportTimeBasis.Calendar,
        };

    public static ReportTimeApply ParseApply(string raw) =>
        raw.Trim().ToLowerInvariant() switch
        {
            "measure" => ReportTimeApply.Measure,
            "both" => ReportTimeApply.Both,
            _ => ReportTimeApply.Clip,
        };
}
