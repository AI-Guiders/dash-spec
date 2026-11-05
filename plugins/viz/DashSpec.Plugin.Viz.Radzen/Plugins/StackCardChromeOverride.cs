using DashSpec.Viz;

namespace DashSpec.Plugin.Viz.RadzenUI.Plugins;

/// <summary>
/// ADR-0112: on the Radzen stack the card chrome binds to native Radzen controls —
/// the view switcher to RadzenDropDown, the extension buttons to RadzenButton.
/// </summary>
public sealed class StackCardChromeOverride : ICardChromeControlsOverride
{
    public Type? ResolveViewSwitch() => typeof(Components.RadzenCardViewSwitch);

    public Type? ResolveButton() => typeof(Components.RadzenCardButton);
}
