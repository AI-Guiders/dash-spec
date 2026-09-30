namespace DashSpec.Host.Configuration;

public sealed class ReportTimeTomlSection
{
    public string TimeBasis { get; set; } = string.Empty;

    public string TimeApply { get; set; } = string.Empty;

    public string WorkTimeColumn { get; set; } = string.Empty;

    public string WorkTimeZone { get; set; } = string.Empty;

    public string WorkStart { get; set; } = string.Empty;

    public string WorkEnd { get; set; } = string.Empty;

    public string WorkDays { get; set; } = string.Empty;

    public Dictionary<string, string> ToSettingsDictionary()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Add(dict, "time_basis", TimeBasis);
        Add(dict, "time_apply", TimeApply);
        Add(dict, "work_time_column", WorkTimeColumn);
        Add(dict, "work_timezone", WorkTimeZone);
        Add(dict, "work_start", WorkStart);
        Add(dict, "work_end", WorkEnd);
        Add(dict, "work_days", WorkDays);
        return dict;
    }

    public void ApplyFromSettings(IReadOnlyDictionary<string, string> settings)
    {
        if (settings.TryGetValue("time_basis", out var basis))
        {
            TimeBasis = basis;
        }

        if (settings.TryGetValue("time_apply", out var apply))
        {
            TimeApply = apply;
        }

        if (settings.TryGetValue("work_time_column", out var column))
        {
            WorkTimeColumn = column;
        }

        if (settings.TryGetValue("work_timezone", out var tz))
        {
            WorkTimeZone = tz;
        }

        if (settings.TryGetValue("work_start", out var start))
        {
            WorkStart = start;
        }

        if (settings.TryGetValue("work_end", out var end))
        {
            WorkEnd = end;
        }

        if (settings.TryGetValue("work_days", out var days))
        {
            WorkDays = days;
        }
    }

    private static void Add(Dictionary<string, string> dict, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            dict[key] = value.Trim();
        }
    }
}
