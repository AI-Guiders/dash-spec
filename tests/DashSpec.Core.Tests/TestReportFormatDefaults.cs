using Microsoft.FSharp.Core;

namespace DashSpec.Core.Tests;

/// <summary>Test-data builder for the F# report-format IR record.</summary>
internal static class TestReportFormatDefaults
{
    public static DashSpec.Modeling.Parse.Document.ReportFormatDefaults Create(
        string? timeFormat = null,
        string? dateFormat = null,
        string? dateTimeFormat = null,
        string? culture = null) =>
        new()
        {
            TimeFormat = ToOption(timeFormat),
            DateFormat = ToOption(dateFormat),
            DateTimeFormat = ToOption(dateTimeFormat),
            Culture = ToOption(culture),
        };

    private static FSharpOption<string> ToOption(string? value) =>
        value is null ? FSharpOption<string>.None : FSharpOption<string>.Some(value);
}
