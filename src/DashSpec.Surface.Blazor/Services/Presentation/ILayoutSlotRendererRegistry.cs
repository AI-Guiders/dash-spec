using DashSpec.Execution.Runtime;
using DashSpec.Modeling.Parse.Layout;

namespace DashSpec.Surface.Blazor.Services.Presentation;

/// <summary>Supported (scope, content kind) pairs for host slot dispatch (ADR-0072 M5).</summary>
public interface ILayoutSlotRendererRegistry
{
    bool Supports(LayoutSlotScope scope, LayoutSlotContentKind contentKind);
}
