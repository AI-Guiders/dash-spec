namespace DashSpec.Core.Model;

public sealed record TabDefinition(
    string Id,
    string? Label,
    IReadOnlyList<string> CardIds,
    string? ModulePath = null,
    LayoutBoardDefinition? LayoutBoard = null);
