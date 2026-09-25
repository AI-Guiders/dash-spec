using System.Globalization;
using DashSpec.Core.Model;
using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class ReportFormatReadingGuideTests
{
    [Fact]
    public void Build_uses_report_defaults_and_moscow_zone()
    {
        var text = ReportFormatReadingGuide.Build(
            new ReportFormatDefaults(TimeFormat: "HH:mm", DateFormat: "dd.MM"),
            TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "Russian Standard Time" : "Europe/Moscow"));

        Assert.Contains("дд.мм", text);
        Assert.Contains("03.08", text);
        Assert.Contains("14:05", text);
        Assert.Contains("МСК", text);
    }

    [Fact]
    public void Build_falls_back_to_short_presets_when_defaults_empty()
    {
        var text = ReportFormatReadingGuide.Build(ReportFormatDefaults.Empty, null);
        Assert.Contains("дд.мм", text);
        Assert.Contains("ЧЧ:мм", text);
        Assert.Contains("UTC", text);
    }
}
