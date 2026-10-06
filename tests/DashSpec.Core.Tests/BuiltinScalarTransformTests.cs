using DashSpec.Core.Transforms;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class BuiltinScalarTransformTests
{
    private static readonly DateTime Reference = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Iana_to_timeshift_normalizes_europe_moscow()
    {
        var raw = new Dictionary<string, string> { ["iana"] = "Europe/Moscow" };

        Assert.True(
            IanaToTimeShiftTransform.TryNormalizeParameters(raw, Reference, out var normalized, out var error));

        Assert.Null(error);
        Assert.Equal("180", normalized.First(p => p.Key == "offset_minutes").Value);
    }

    [Fact]
    public void Timeshift_to_iana_is_lossy_but_resolves()
    {
        var raw = new Dictionary<string, string> { ["zone"] = "UTC+3" };

        Assert.True(
            TimeShiftToIanaTransform.TryNormalizeParameters(raw, Reference, out var normalized, out var error));

        Assert.Null(error);
        Assert.Equal("true", normalized.First(p => p.Key == "lossy").Value);
        Assert.False(string.IsNullOrWhiteSpace(normalized.First(p => p.Key == "iana").Value));
    }
}
