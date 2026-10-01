namespace DashSpec.Core.Layout;

/// <summary>What occupies a grid slot; dispatch key for host/plugin renderers (ADR-0072).</summary>
public enum LayoutSlotContentKind
{
    Card,
    Nest,
    Group,
    Filter,
    PrimaryDiagram,
    SecondaryDiagram,
}
