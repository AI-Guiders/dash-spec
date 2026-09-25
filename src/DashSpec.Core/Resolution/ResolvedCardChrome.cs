namespace DashSpec.Core.Resolution;

/// <summary>Card title for chrome vs accessibility/export (ADR-0057 §4 <c>card.chrome_title</c>).</summary>
public sealed record ResolvedCardChrome(
    string Title,
    bool ShowChromeTitle)
{
    public static ResolvedCardChrome Visible(string title) => new(title, true);

    public static ResolvedCardChrome Suppressed(string title) => new(title, false);
}
