namespace DashSpec.Core.Model;

/// <summary>Named inner layout board referenced from a bracket cell (ADR-0063).</summary>
public sealed record LayoutBoardNestDefinition(
    string Id,
    IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>Declaration of a nest on a layout board (does not consume an outer grid row).</summary>
public sealed record LayoutBoardNestRow(LayoutBoardNestDefinition Nest) : LayoutBoardEntry;
