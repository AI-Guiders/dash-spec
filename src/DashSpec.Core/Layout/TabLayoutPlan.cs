using DashSpec.Core.Model;

namespace DashSpec.Core.Layout;

/// <summary>Resolved placements for a tab layout board including optional card groups (ADR-0056).</summary>
public sealed record TabLayoutPlan(
    IReadOnlyList<LayoutBoardEntry> Entries,
    IReadOnlyDictionary<string, PlacementDefinition> TopLevelPlacements,
    IReadOnlyDictionary<string, LayoutGroupPlacement> Groups,
    IReadOnlyDictionary<string, LayoutNestPlacement> Nests,
    IReadOnlyDictionary<string, PlacementDefinition> AllCardPlacements);

/// <summary>Outer grid row wrapper with inner card placements.</summary>
public sealed record LayoutGroupPlacement(
    string Id,
    string? Title,
    int OuterRow,
    IReadOnlyDictionary<string, PlacementDefinition> InnerPlacements);

/// <summary>Nested grid inside one bracket cell (ADR-0063).</summary>
public sealed record LayoutNestPlacement(
    string Id,
    PlacementDefinition OuterPlacement,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    IReadOnlyDictionary<string, PlacementDefinition> InnerPlacements);
