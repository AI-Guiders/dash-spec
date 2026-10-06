using DashSpec.Core;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class UtcOffsetZoneLiteralTests
{
    [Theory]
    [InlineData("UTC", 0)]
    [InlineData("utc+0", 0)]
    [InlineData("UTC+3", 180)]
    [InlineData("UTC+03:30", 210)]
    [InlineData("UTC-5", -300)]
    public void TryParse_accepts_fixed_utc_offsets(string zone, int expectedMinutes)
    {
        Assert.True(UtcOffsetZoneLiteral.TryParse(zone, out var parsed, out var error));
        Assert.Null(error);
        Assert.Equal(expectedMinutes, parsed.TotalMinutes);
    }

    [Theory]
    [InlineData("Europe/Moscow")]
    [InlineData("Russian Standard Time")]
    public void TryParse_rejects_named_zones(string zone)
    {
        Assert.False(UtcOffsetZoneLiteral.TryParse(zone, out _, out var error));
        Assert.NotNull(error);
    }
}
