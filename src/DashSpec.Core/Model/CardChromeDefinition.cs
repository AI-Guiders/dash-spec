namespace DashSpec.Core.Model;

public enum CardBoundFilterChrome
{
    Chips,
    Hidden,
    ToolbarOnly,
}

public sealed record CardChromeDefinition(CardBoundFilterChrome BoundFilters = CardBoundFilterChrome.Chips);
