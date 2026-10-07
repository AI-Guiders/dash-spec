using DashSpec.Core.Layout;

namespace DashSpec.Surface.Blazor.Services.Presentation;

public sealed class LayoutSlotRendererRegistry : ILayoutSlotRendererRegistry
{
    private static readonly HashSet<(LayoutSlotScope Scope, LayoutSlotContentKind Kind)> Supported =
    [
        (LayoutSlotScope.HostPageToolbar, LayoutSlotContentKind.Filter),
        (LayoutSlotScope.HostTabBoard, LayoutSlotContentKind.Card),
        (LayoutSlotScope.HostTabBoard, LayoutSlotContentKind.Nest),
        (LayoutSlotScope.HostTabBoard, LayoutSlotContentKind.Group),
        (LayoutSlotScope.CardInterior, LayoutSlotContentKind.Filter),
        (LayoutSlotScope.CardInterior, LayoutSlotContentKind.PrimaryDiagram),
        (LayoutSlotScope.CardInterior, LayoutSlotContentKind.SecondaryDiagram),
    ];

    public bool Supports(LayoutSlotScope scope, LayoutSlotContentKind contentKind) =>
        Supported.Contains((scope, contentKind));

    public IReadOnlyCollection<(LayoutSlotScope Scope, LayoutSlotContentKind Kind)> AllSupported() => Supported;
}
