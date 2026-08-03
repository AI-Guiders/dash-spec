namespace DashSpec.Core.Model;

/// <summary>Analytics screen inside a report (ADR-0030).</summary>
public sealed record ReportPageDefinition(
    string Id,
    string? Title,
    LayoutBoardDefinition? LayoutBoard = null,
    string? TabId = null,
    LayoutBoardDefinition? ToolbarBoard = null,
    FilterDeriveDefinition? UsageDateDerive = null);
