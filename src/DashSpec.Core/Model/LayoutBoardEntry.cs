namespace DashSpec.Core.Model;

/// <summary>Top-level layout board entry: a card row or a visual card group (ADR-0056).</summary>
public abstract record LayoutBoardEntry;

/// <summary>Bracket row <c>[ card … ]</c> on the dashboard grid.</summary>
public sealed record LayoutBoardCardRow(IReadOnlyList<string> CardIds) : LayoutBoardEntry;

/// <summary>GroupBox-style wrapper with inner bracket rows.</summary>
public sealed record LayoutBoardGroupRow(LayoutBoardGroupDefinition Group) : LayoutBoardEntry;

/// <summary>Grouped cards sharing a title and inner layout board.</summary>
public sealed record LayoutBoardGroupDefinition(
    string Id,
    string? Title,
    IReadOnlyList<IReadOnlyList<string>> Rows);
