namespace DashSpec.Execution.Runtime;

/// <summary>Where a placement grid lives (ADR-0072).</summary>
public enum LayoutSlotScope
{
    HostTabBoard,
    HostPageToolbar,
    CardInterior,
    CardFilterChrome,
}
