namespace DashSpec.Core.Model;

/// <summary>Declaration of a nest on a layout board (does not consume an outer grid row).</summary>
public sealed record LayoutBoardNestRow(LayoutBoardNestDefinition Nest) : LayoutBoardEntry;
