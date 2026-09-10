namespace DashSpec.Host.Services.Presentation;

/// <summary>Remark 7: long field filters (e.g. app whitelist &gt;80 products).</summary>
public static class FilterLargeListOptions
{
    public const int Threshold = 80;

    public const string Scroll = "scroll";

    public const string Expand = "expand";

    public static string Normalize(string? raw) =>
        string.Equals(raw?.Trim(), Expand, StringComparison.OrdinalIgnoreCase) ? Expand : Scroll;

    public static bool IsExpand(string? layout) =>
        string.Equals(Normalize(layout), Expand, StringComparison.OrdinalIgnoreCase);

    public static bool IsLargeList(int optionCount) => optionCount > Threshold;
}
