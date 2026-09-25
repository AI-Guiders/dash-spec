namespace DashSpec.Execution.Runtime;

public sealed record ReportFormatGuideRow(string Label, string Pattern, string Example);

public sealed record ReportFormatReadingGuideModel(
    IReadOnlyList<ReportFormatGuideRow> Rows,
    string TimeZoneLabel);
