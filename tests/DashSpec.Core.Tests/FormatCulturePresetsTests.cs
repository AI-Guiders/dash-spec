using System.Globalization;
using DashSpec.Core.Model;
using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class FormatCulturePresetsTests
{
    [Fact]
    public void ResolveCulture_prefers_report_over_ui()
    {
        var culture = FormatCulturePresets.ResolveCulture(
            new ReportFormatDefaults(Culture: "en-GB"),
            CultureInfo.GetCultureInfo("ru-RU"));
        Assert.Equal("en-GB", culture.Name);
    }

    [Theory]
    [InlineData("ru-RU", "03.08")]
    [InlineData("en-US", "8/3")]
    [InlineData("en-GB", "03/08")]
    public void Date_short_preset_follows_culture(string cultureName, string expected)
    {
        LabelFormat.ClearReportDefaults();
        LabelFormat.UiCulture = CultureInfo.GetCultureInfo(cultureName);
        try
        {
            var text = LabelFormat.FormatObject(new DateOnly(2026, 8, 3), "date.short");
            Assert.Equal(expected, text);
        }
        finally
        {
            LabelFormat.UiCulture = null;
        }
    }

    [Fact]
    public void Report_culture_overrides_host_ui_for_presets()
    {
        LabelFormat.UiCulture = CultureInfo.GetCultureInfo("en-US");
        LabelFormat.SetReportDefaults(new ReportFormatDefaults(Culture: "ru-RU"));
        try
        {
            var text = LabelFormat.FormatObject(new DateOnly(2026, 8, 3), "date.short");
            Assert.Equal("03.08", text);
        }
        finally
        {
            LabelFormat.ClearReportDefaults();
            LabelFormat.UiCulture = null;
        }
    }
}
