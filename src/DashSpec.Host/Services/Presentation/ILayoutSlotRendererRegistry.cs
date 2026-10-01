using DashSpec.Core.Layout;

namespace DashSpec.Host.Services.Presentation;

/// <summary>Supported (scope, content kind) pairs for host slot dispatch (ADR-0072 M5).</summary>
public interface ILayoutSlotRendererRegistry
{
    bool Supports(LayoutSlotScope scope, LayoutSlotContentKind contentKind);
}
