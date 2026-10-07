namespace DashSpec.Presentation.Filters;

/// <inheritdoc cref="DashSpec.Core.Filters.FilterLargeListOptions"/>
public static class FilterLargeListOptions
{
    public const int Threshold = Core.Filters.FilterLargeListOptions.Threshold;

    public const string Scroll = Core.Filters.FilterLargeListOptions.Scroll;

    public const string Expand = Core.Filters.FilterLargeListOptions.Expand;

    public static string Normalize(string? raw) => Core.Filters.FilterLargeListOptions.Normalize(raw);

    public static bool IsExpand(string? layout) => Core.Filters.FilterLargeListOptions.IsExpand(layout);

    public static bool IsLargeList(int optionCount) => Core.Filters.FilterLargeListOptions.IsLargeList(optionCount);
}
