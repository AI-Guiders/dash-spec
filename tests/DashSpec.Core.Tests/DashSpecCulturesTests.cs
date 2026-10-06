using System.Globalization;
using DashSpec.Core.Localization;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class DashSpecCulturesTests
{
    [Theory]
    [InlineData("en", "en-US")]
    [InlineData("ru", "ru-RU")]
    [InlineData("en-GB", "en-GB")]
    [InlineData(null, "ru-RU")]
    public void Resolve_maps_host_language_tokens(string? token, string expectedName)
    {
        Assert.Equal(expectedName, DashSpecCultures.Resolve(token).Name);
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
        Assert.Equal(new DateTime(2026, 9, 22), ruMonday.Date);
    }
}
