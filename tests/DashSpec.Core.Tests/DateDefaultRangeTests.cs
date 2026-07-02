using DashSpec.Abstractions.Query;
using DashSpec.Core.Compilation;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Resolution;
using DashSpec.Core.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public class DateDefaultRangeTests
{
    [Theory]
    [InlineData("-7d..today", -7, 0)]
    [InlineData("-1d..today", -1, 0)]
    [InlineData("today..today", 0, 0)]
    public void Resolve_relative_ranges(string expression, int fromOffset, int toOffset)
    {
        var today = new DateOnly(2026, 6, 24);
        var range = DateDefaultRange.Resolve(expression, today);
        Assert.Equal(today.AddDays(fromOffset), range.From);
        Assert.Equal(today.AddDays(toOffset), range.To);
    }

    [Fact]
    public void Resolve_absolute_range()
    {
        var range = DateDefaultRange.Resolve("2026-06-01..2026-06-07", new DateOnly(2026, 6, 24));
        Assert.Equal(new DateOnly(2026, 6, 1), range.From);
        Assert.Equal(new DateOnly(2026, 6, 7), range.To);
    }

    [Fact]
    public void Parse_rejects_magic_preset_names()
    {
        var ex = Assert.ThrowsAny<Exception>(() =>
            DashSpecParser.Parse("""
                @dashboard t
                dashboard "T" {
                  filter date usage_date {
                    column = usage_date as "Usage"
                    default = last_7_days
                  }
                }
                """));
        Assert.Contains("..", ex.Message);
    }
}
