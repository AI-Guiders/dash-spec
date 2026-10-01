using DashSpec.Core.Model;

namespace DashSpec.Core.Layout;

/// <summary>Plans ordered slot descriptors per scope (ADR-0072). Composition only — no SQL/render.</summary>
public static class LayoutSlotEngine
{
    public static IReadOnlyList<LayoutSlotDescriptor> PlanCardInterior(
        IReadOnlyDictionary<string, PlacementDefinition>? interiorPlacements,
        IReadOnlyList<string>? localFilters,
        IReadOnlyCollection<string>? secondaryDiagramSlotRefs) =>
        PlanScoped(
            LayoutSlotScope.CardInterior,
            interiorPlacements,
            token => ClassifyCardInterior(token, localFilters, secondaryDiagramSlotRefs));

    public static IReadOnlyList<LayoutSlotDescriptor> PlanHostPageToolbar(
        IReadOnlyList<string> orderedFilterNames,
        IReadOnlyDictionary<string, PlacementDefinition> placements)
    {
        if (orderedFilterNames.Count == 0)
        {
            return [];
        }

        var list = new List<LayoutSlotDescriptor>(orderedFilterNames.Count);
        foreach (var name in orderedFilterNames)
        {
            if (!placements.TryGetValue(name, out var placement))
            {
                placement = new PlacementDefinition(0, 0, 1);
            }

            list.Add(new LayoutSlotDescriptor(
                name,
                placement,
                LayoutSlotScope.HostPageToolbar,
                LayoutSlotContentKind.Filter));
        }

        return list;
    }

    public static IReadOnlyList<LayoutSlotDescriptor> PlanHostTabBoard(
        TabLayoutPlan plan,
        int columns,
        Func<string, string> resolveCardRef)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(resolveCardRef);
        if (columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }

        var slots = new List<LayoutSlotDescriptor>();
        foreach (var entry in plan.Entries)
        {
            switch (entry)
            {
                case LayoutBoardCardRow cardRow:
                    foreach (var token in cardRow.CardIds)
                    {
                        var refId = LayoutBoardCellFormat.RefToken(token);
                        if (plan.Nests.TryGetValue(refId, out var nest))
                        {
                            slots.Add(new LayoutSlotDescriptor(
                                refId,
                                nest.OuterPlacement,
                                LayoutSlotScope.HostTabBoard,
                                LayoutSlotContentKind.Nest));
                            continue;
                        }

                        var cardId = resolveCardRef(token);
                        if (plan.TopLevelPlacements.TryGetValue(cardId, out var placement))
                        {
                            slots.Add(new LayoutSlotDescriptor(
                                cardId,
                                placement,
                                LayoutSlotScope.HostTabBoard,
                                LayoutSlotContentKind.Card));
                        }
                    }

                    break;
                case LayoutBoardGroupRow { Group: var group }:
                    if (plan.Groups.TryGetValue(group.Id, out var groupPlacement))
                    {
                        slots.Add(new LayoutSlotDescriptor(
                            group.Id,
                            new PlacementDefinition(groupPlacement.OuterRow, 1, columns),
                            LayoutSlotScope.HostTabBoard,
                            LayoutSlotContentKind.Group));
                    }

                    break;
            }
        }

        return slots;
    }

    public static IReadOnlyList<LayoutSlotDescriptor> PlanHostTabBoardCards(
        IReadOnlyList<(string CardId, DiagramDataFamily DataFamily)> cardsInOrder,
        IReadOnlyDictionary<string, PlacementDefinition> tabPlacements,
        int columns)
    {
        ArgumentNullException.ThrowIfNull(cardsInOrder);
        ArgumentNullException.ThrowIfNull(tabPlacements);
        if (columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }

        var slots = new List<LayoutSlotDescriptor>(cardsInOrder.Count);
        foreach (var (cardId, dataFamily) in cardsInOrder)
        {
            if (!tabPlacements.TryGetValue(cardId, out var placement))
            {
                placement = PlacementDefaults.ForFamily(dataFamily, columns);
            }

            slots.Add(new LayoutSlotDescriptor(
                cardId,
                placement,
                LayoutSlotScope.HostTabBoard,
                LayoutSlotContentKind.Card));
        }

        return slots;
    }

    internal static IReadOnlyList<LayoutSlotDescriptor> PlanScoped(
        LayoutSlotScope scope,
        IReadOnlyDictionary<string, PlacementDefinition>? placements,
        Func<string, LayoutSlotContentKind> classify)
    {
        if (placements is not { Count: > 0 })
        {
            return [];
        }

        return OrderPlacements(placements)
            .Select(kv => new LayoutSlotDescriptor(kv.Key, kv.Value, scope, classify(kv.Key)))
            .ToList();
    }

    internal static IReadOnlyList<KeyValuePair<string, PlacementDefinition>> OrderPlacements(
        IReadOnlyDictionary<string, PlacementDefinition> placements) =>
        placements
            .OrderBy(static kv => kv.Value.Row)
            .ThenBy(static kv => kv.Value.Col)
            .ToList();

    private static LayoutSlotContentKind ClassifyCardInterior(
        string token,
        IReadOnlyList<string>? localFilters,
        IReadOnlyCollection<string>? secondaryDiagramSlotRefs)
    {
        if (localFilters?.Contains(token, StringComparer.OrdinalIgnoreCase) == true)
        {
            return LayoutSlotContentKind.Filter;
        }

        var secondary = secondaryDiagramSlotRefs is null
            ? null
            : new HashSet<string>(secondaryDiagramSlotRefs, StringComparer.OrdinalIgnoreCase);

        if (secondary?.Contains(token) == true)
        {
            return LayoutSlotContentKind.SecondaryDiagram;
        }

        return LayoutSlotContentKind.PrimaryDiagram;
    }
}
