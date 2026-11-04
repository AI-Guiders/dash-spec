namespace DashSpec.Viz;

/// <summary>One view of a card's view switcher (ADR-0118 addressability: views-{cardId}).</summary>
public sealed record CardViewOption(string Id, string Label);

/// <summary>
/// ADR-0112: the card chrome controls (the view-switcher selector, the extension
/// buttons) are stack-replaceable visuals. The surface owns the semantics — the view
/// ids, the active view, the action wiring; a stack may bind its own controls
/// (RadzenDropDown / RadzenButton) implementing these parameter surfaces. Absent
/// override — the surface's native markup renders (the selfmade stack path).
/// </summary>
public interface ICardChromeControlsOverride
{
    /// <summary>Component for the view-switcher surface: Views (IReadOnlyList&lt;CardViewOption&gt;),
    /// Active (string), ActiveChanged (EventCallback&lt;string&gt;), Disabled, AriaLabel.
    /// Null — keep the native select markup.</summary>
    Type? ResolveViewSwitch();

    /// <summary>Component for the button surface: Label, Disabled, Clicked (EventCallback).
    /// Null — keep the native button markup.</summary>
    Type? ResolveButton();
}

/// <summary>Default: no overrides (the surface's native chrome markup renders).</summary>
public sealed class DefaultCardChromeControlsOverride : ICardChromeControlsOverride
{
    public Type? ResolveViewSwitch() => null;

    public Type? ResolveButton() => null;
}
