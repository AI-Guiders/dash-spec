using DashSpec.Execution.Runtime;
using DashSpec.Modeling.Core;
using DashSpec.Modeling.Parse.Layout;

namespace DashSpec.Surface.Blazor.Services.Presentation;

public sealed class LayoutSlotRendererRegistry : ILayoutSlotRendererRegistry
{
    private static readonly HashSet<(LayoutSlotScope Scope, WiringNodeKind Kind)> Supported =
    [
        (LayoutSlotScope.HostPageToolbar, WiringNodeKind.Filter),
        (LayoutSlotScope.HostTabBoard, WiringNodeKind.Card),
        (LayoutSlotScope.HostTabBoard, WiringNodeKind.Nest),
        (LayoutSlotScope.HostTabBoard, WiringNodeKind.Group),
        (LayoutSlotScope.CardInterior, WiringNodeKind.Filter),
        (LayoutSlotScope.CardInterior, WiringNodeKind.Slot),
    ];

    public bool Supports(LayoutSlotScope scope, WiringNodeKind kind) =>
        Supported.Contains((scope, kind));

    public IReadOnlyCollection<(LayoutSlotScope Scope, WiringNodeKind Kind)> AllSupported() => Supported;
}
