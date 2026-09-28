using Microsoft.AspNetCore.Components;

namespace DashSpec.Viz;

public sealed class VizCardToolbarContext
{
    public required CardRenderResult Card { get; init; }

    public required EventCallback<CardActionRequest> OnCardAction { get; init; }
}
