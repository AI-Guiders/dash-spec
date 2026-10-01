namespace DashSpec.Core.Model;

/// <summary>Dashboard-level filter toolbar chrome (layout, sticky, apply mode).</summary>
public sealed record FiltersChromeDefinition(
    string Layout = "card",
    string Sticky = "none",
    string Apply = "manual",
    string ApplyControl = "icon",
    int DebounceMs = 400,
    string FormatGuide = "hidden",
    string Cells = "")
{
    public const string ToolbarCellsLabeled = "labeled";
    public const string ToolbarCellsInline = "inline";

    public const string StickyNone = "none";
    public const string StickyLine = "line";
    public const string StickyCard = "card";
    public const string FormatGuideHidden = "hidden";
    public const string FormatGuideShow = "show";

    public const string ApplyControlIcon = "icon";
    public const string ApplyControlButton = "button";

    public static FiltersChromeDefinition Default { get; } = new();

    public bool IsApplyIconControl =>
        string.Equals(ApplyControl, ApplyControlIcon, StringComparison.OrdinalIgnoreCase);

    public bool ShowsFormatGuide =>
        string.Equals(FormatGuide, FormatGuideShow, StringComparison.OrdinalIgnoreCase);

    public bool IsAutoApply => string.Equals(Apply, "auto", StringComparison.OrdinalIgnoreCase);

    public bool IsBarLayout => string.Equals(Layout, "bar", StringComparison.OrdinalIgnoreCase);

    public bool IsSticky => !string.Equals(Sticky, StickyNone, StringComparison.OrdinalIgnoreCase);

    public bool IsStickyLine => string.Equals(Sticky, StickyLine, StringComparison.OrdinalIgnoreCase);

    public bool IsStickyCard => string.Equals(Sticky, StickyCard, StringComparison.OrdinalIgnoreCase);

    /// <summary>Bar toolbar: label row + control row (ADR-0022 inline cells). Off when <c>cells = inline</c>.</summary>
    public bool IsToolbarLabeledCells =>
        IsBarLayout &&
        !string.Equals(Cells, ToolbarCellsInline, StringComparison.OrdinalIgnoreCase);

    public bool ToolbarShowDateLabel => IsToolbarLabeledCells;

    public string ToolbarDateLabelClass =>
        IsToolbarLabeledCells ? "filter-inline-label" : "filter-label";
}
