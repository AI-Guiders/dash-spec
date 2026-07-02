namespace DashSpec.Core.Model;

public sealed record TabDefinition(
    string Id,
    string? Label,
    IReadOnlyList<string> CardIds,
    string? DashspecPath = null,
    LayoutBoardDefinition? LayoutBoard = null);
