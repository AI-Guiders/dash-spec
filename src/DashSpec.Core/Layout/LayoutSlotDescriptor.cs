using DashSpec.Core.Model;

namespace DashSpec.Core.Layout;

/// <summary>One cell in a layout slot plane: token, placement, scope, and content kind.</summary>
public sealed record LayoutSlotDescriptor(
    string Token,
    PlacementDefinition Placement,
    LayoutSlotScope Scope,
    LayoutSlotContentKind ContentKind);
