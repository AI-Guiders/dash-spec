namespace DashSpec.Presentation.Filters;

/// <inheritdoc cref="DashSpec.Execution.Runtime.FilterLargeListOptions"/>
public static class FilterLargeListOptions
{
    public const int Threshold = Execution.Runtime.FilterLargeListOptions.Threshold;

    public const string Scroll = Execution.Runtime.FilterLargeListOptions.Scroll;

    public const string Expand = Execution.Runtime.FilterLargeListOptions.Expand;

    public static string Normalize(string? raw) => Execution.Runtime.FilterLargeListOptions.Normalize(raw);

    public static bool IsExpand(string? layout) => Execution.Runtime.FilterLargeListOptions.IsExpand(layout);

    public static bool IsLargeList(int optionCount) => Execution.Runtime.FilterLargeListOptions.IsLargeList(optionCount);
}
