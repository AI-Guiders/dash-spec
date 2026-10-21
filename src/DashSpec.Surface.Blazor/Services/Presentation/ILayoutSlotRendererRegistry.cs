using DashSpec.Execution.Runtime;
using DashSpec.Modeling.Core;
using DashSpec.Modeling.Parse.Layout;

namespace DashSpec.Surface.Blazor.Services.Presentation;

/// <summary>Supported (scope, node kind) pairs for host slot dispatch (ADR-0072 M5 / ADR-0111 P1: the plane's slot kind).</summary>
public interface ILayoutSlotRendererRegistry
{
    bool Supports(LayoutSlotScope scope, WiringNodeKind kind);
}
