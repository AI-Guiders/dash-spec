using Microsoft.FSharp.Core;

namespace DashSpec.Core.Resolution;

/// <summary>Parses host/runtime overrides for the F# report-time policy IR.</summary>
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
        if (properties.Count == 0 || !ConfigurationKeys.Any(properties.ContainsKey))
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

        var baseCalendar = basePolicy.WorkCalendar?.Value;
        var overlayCalendar = overlay.WorkCalendar?.Value;
        var calendar = overlayCalendar ?? baseCalendar;
        if (overlayCalendar is not null && baseCalendar is not null)
        {
            calendar = CreateCalendar(
                overlayCalendar.TimeZoneId?.Value ?? baseCalendar.TimeZoneId?.Value,
                overlayCalendar.StartTime?.Value ?? baseCalendar.StartTime?.Value,
                overlayCalendar.EndTime?.Value ?? baseCalendar.EndTime?.Value,
                overlayCalendar.WorkDays?.Value ?? baseCalendar.WorkDays?.Value);
        }

        return CreatePolicy(
            overlay.Basis,
            overlay.Apply,
            overlay.WorkTimeColumn?.Value ?? basePolicy.WorkTimeColumn?.Value,
            calendar);
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

        var workColumn = existing?.WorkTimeColumn?.Value;
        if (properties.TryGetValue("work_time_column", out var columnRaw) && !string.IsNullOrWhiteSpace(columnRaw))
        {
            workColumn = columnRaw.Trim();
        }

        var calendar = existing?.WorkCalendar?.Value;
        if ((properties.TryGetValue("work_timezone", out var tz) && !string.IsNullOrWhiteSpace(tz))
            || properties.ContainsKey("work_start")
            || properties.ContainsKey("work_end")
            || properties.ContainsKey("work_days"))
        {
            calendar = CreateCalendar(
                ReadValue(properties, "work_timezone") ?? calendar?.TimeZoneId?.Value,
                ReadValue(properties, "work_start") ?? calendar?.StartTime?.Value,
                ReadValue(properties, "work_end") ?? calendar?.EndTime?.Value,
                ReadValue(properties, "work_days") ?? calendar?.WorkDays?.Value);
        }

        return CreatePolicy(basis, apply, workColumn, calendar);
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

    private static string? ReadValue(IReadOnlyDictionary<string, string> properties, string key) =>
        properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static ReportTimePolicy CreatePolicy(
        ReportTimeBasis basis,
        ReportTimeApply apply,
        string? workTimeColumn,
        WorkCalendarDefinition? calendar) =>
        new()
        {
            Basis = basis,
            Apply = apply,
            WorkTimeColumn = ToOption(workTimeColumn),
            WorkCalendar = calendar is null
                ? FSharpOption<WorkCalendarDefinition>.None
                : FSharpOption<WorkCalendarDefinition>.Some(calendar),
        };

    private static WorkCalendarDefinition CreateCalendar(
        string? timeZoneId,
        string? startTime,
        string? endTime,
        string? workDays) =>
        new()
        {
            TimeZoneId = ToOption(timeZoneId),
            StartTime = ToOption(startTime),
            EndTime = ToOption(endTime),
            WorkDays = ToOption(workDays),
        };

    private static FSharpOption<string> ToOption(string? value) =>
        value is null ? FSharpOption<string>.None : FSharpOption<string>.Some(value);
}
