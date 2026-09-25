using DashSpec.Core.Model;

namespace DashSpec.Core.Layout;

/// <summary>Resolved placements for a tab layout board including optional card groups (ADR-0056).</summary>
public sealed record TabLayoutPlan(
    IReadOnlyList<LayoutBoardEntry> Entries,
    IReadOnlyDictionary<string, PlacementDefinition> TopLevelPlacements,
    IReadOnlyDictionary<string, LayoutGroupPlacement> Groups,
    IReadOnlyDictionary<string, PlacementDefinition> AllCardPlacements);

/// <summary>Outer grid row wrapper with inner card placements.</summary>
public sealed record LayoutGroupPlacement(
    string Id,
    string? Title,
    int OuterRow,
    IReadOnlyDictionary<string, PlacementDefinition> InnerPlacements);
