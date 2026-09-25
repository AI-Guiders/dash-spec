using System.Globalization;
using DashSpec.Core.Model;
using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class ReportFormatReadingGuideTests
{
    [Fact]
    public void BuildModel_uses_report_defaults_and_moscow_zone()
    {
        var model = ReportFormatReadingGuide.BuildModel(
            new ReportFormatDefaults(TimeFormat: "HH:mm", DateFormat: "dd.MM"),
            TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "Russian Standard Time" : "Europe/Moscow"));

        Assert.Equal("дд.мм", model.Rows[0].Pattern);
        Assert.Equal("03.08", model.Rows[0].Example);
        Assert.Equal("ЧЧ:mm", model.Rows[1].Pattern);
        Assert.Equal("14:05", model.Rows[1].Example);
        Assert.Contains("МСК", model.TimeZoneLabel);
    }

    [Fact]
    public void BuildModel_falls_back_to_short_presets_when_defaults_empty()
    {
        var model = ReportFormatReadingGuide.BuildModel(ReportFormatDefaults.Empty, null);
        Assert.Equal("дд.мм", model.Rows[0].Pattern);
        Assert.Equal("ЧЧ:мм", model.Rows[1].Pattern);
        Assert.Equal("UTC", model.TimeZoneLabel);
    }

    [Fact]
    public void Build_keeps_compact_string_for_tooltips()
    {
        var text = ReportFormatReadingGuide.Build(ReportFormatDefaults.Empty, null);
        Assert.Contains("дд.мм", text);
        Assert.Contains("ЧЧ:мм", text);
    }
}
