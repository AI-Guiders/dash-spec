namespace DashSpec.Core.Model;

public sealed record ReportFormatDefaults(
    string? TimeFormat = null,
    string? DateFormat = null,
    string? DateTimeFormat = null,
    string? Culture = null)
{
    public static ReportFormatDefaults Empty { get; } = new();
}
