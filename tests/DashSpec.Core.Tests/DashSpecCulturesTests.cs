using System.Globalization;
using DashSpec.Core.Localization;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class DashSpecCulturesTests
{
    [Theory]
    [InlineData("ru-RU")]
    [InlineData("en-US")]
    [InlineData("en-GB")]
    public void Parse_accepts_specific_culture_names(string name)
    {
        Assert.Equal(name, DashSpecCultures.Parse(name).Name);
    }

    [Theory]
    [InlineData("ru", "ru-RU")]
    [InlineData("en", "en-US")]
    [InlineData("ru-RU", "ru-RU")]
    public void NormalizeStoredLanguage_upgrades_legacy_shorthand(string stored, string expected)
    {
        Assert.Equal(expected, DashSpecCultures.NormalizeStoredLanguage(stored));
    }

    [Fact]
    public void Parse_accepts_normalized_shorthand()
    {
        Assert.Equal("ru-RU", DashSpecCultures.Parse("ru").Name);
    }

    [Fact]
    public void Parse_rejects_empty()
    {
        Assert.ThrowsAny<Exception>(() => DashSpecCultures.Parse("   "));
    }

    [Fact]
    public void StartOfCalendarWeek_uses_culture_first_day()
    {
        var thursday = new DateTime(2026, 9, 24);
        var usSunday = DashSpecCultures.StartOfCalendarWeek(
            thursday,
            CultureInfo.GetCultureInfo("en-US"));
        var ruMonday = DashSpecCultures.StartOfCalendarWeek(
            thursday,
            CultureInfo.GetCultureInfo("ru-RU"));

        Assert.Equal(DayOfWeek.Sunday, usSunday.DayOfWeek);
        Assert.Equal(new DateTime(2026, 9, 20), usSunday.Date);
        Assert.Equal(DayOfWeek.Monday, ruMonday.DayOfWeek);
        Assert.Equal(new DateTime(2026, 9, 21), ruMonday.Date);
    }
}
