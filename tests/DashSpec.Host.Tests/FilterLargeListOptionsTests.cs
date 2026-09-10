using DashSpec.Host.Services.Presentation;
using Xunit;

namespace DashSpec.Host.Tests;

public sealed class FilterLargeListOptionsTests
{
    [Theory]
    [InlineData(null, FilterLargeListOptions.Scroll)]
    [InlineData("", FilterLargeListOptions.Scroll)]
    [InlineData("scroll", FilterLargeListOptions.Scroll)]
    [InlineData("SCROLL", FilterLargeListOptions.Scroll)]
    [InlineData("expand", FilterLargeListOptions.Expand)]
    [InlineData("EXPAND", FilterLargeListOptions.Expand)]
    [InlineData("combobox", FilterLargeListOptions.Scroll)]
    public void Normalize_maps_to_scroll_or_expand(string? raw, string expected) =>
        Assert.Equal(expected, FilterLargeListOptions.Normalize(raw));

    [Theory]
    [InlineData(80, false)]
    [InlineData(81, true)]
    [InlineData(200, true)]
    public void IsLargeList_uses_strict_threshold(int count, bool expected) =>
        Assert.Equal(expected, FilterLargeListOptions.IsLargeList(count));
}
