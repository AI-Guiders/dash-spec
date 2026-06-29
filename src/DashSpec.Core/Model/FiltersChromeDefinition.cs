namespace DashSpec.Core.Model;

/// <summary>Dashboard-level filter toolbar chrome (layout, sticky, apply mode).</summary>
public sealed record FiltersChromeDefinition(
    string Layout = "card",
    string Sticky = "none",
    string Apply = "manual",
    int DebounceMs = 400)
{
    public const string StickyNone = "none";
    public const string StickyLine = "line";
    public const string StickyCard = "card";

    public static FiltersChromeDefinition Default { get; } = new();

    public bool IsAutoApply => string.Equals(Apply, "auto", StringComparison.OrdinalIgnoreCase);

    public bool IsBarLayout => string.Equals(Layout, "bar", StringComparison.OrdinalIgnoreCase);

    public bool IsSticky => !string.Equals(Sticky, StickyNone, StringComparison.OrdinalIgnoreCase);

    public bool IsStickyLine => string.Equals(Sticky, StickyLine, StringComparison.OrdinalIgnoreCase);

    public bool IsStickyCard => string.Equals(Sticky, StickyCard, StringComparison.OrdinalIgnoreCase);
}

public sealed record TabDefinition(
    string Id,
    string? Label,
    IReadOnlyList<string> CardIds);
