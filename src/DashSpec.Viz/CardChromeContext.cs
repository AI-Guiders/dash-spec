using Microsoft.AspNetCore.Components;

namespace DashSpec.Viz;

/// <summary>
/// Slot the host hands to plugin-provided card chrome components (ADR-0077: plugins are vertical).
/// The host renders the component and passes data; the plugin owns its UI, its copy and its logic.
/// </summary>
public sealed class CardChromeContext
{
    public required CardRenderResult Card { get; init; }

    public required EventCallback<CardActionRequest> OnCardAction { get; init; }
}
