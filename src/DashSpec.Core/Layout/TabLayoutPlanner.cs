using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Resolution;

namespace DashSpec.Core.Layout;

/// <summary>Resolves tab layout boards with optional card groups and nests into a render plan.</summary>
public static class TabLayoutPlanner
{
    public static TabLayoutPlan Plan(
        LayoutBoardDefinition board,
        IReadOnlyList<CardDefinition> tabCards,
        int columns,
        string tabId)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(tabCards);
        if (columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }

        var context = $"Tab '{tabId}' layout";
        var nestDefinitions = CollectNests(board, context);
        var topLevel = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase);
        var allCards = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase);
        var groups = new Dictionary<string, LayoutGroupPlacement>(StringComparer.OrdinalIgnoreCase);
        var nests = new Dictionary<string, LayoutNestPlacement>(StringComparer.OrdinalIgnoreCase);
        var outerRow = 0;

        foreach (var entry in board.Entries)
        {
            switch (entry)
            {
                case LayoutBoardNestRow:
                    continue;
                case LayoutBoardCardRow cardRow:
                    outerRow++;
                    PlaceCardRow(
                        cardRow.CardIds,
                        outerRow,
                        columns,
                        context,
                        tabCards,
                        nestDefinitions,
                        topLevel,
                        allCards,
                        nests);
                    break;
                case LayoutBoardGroupRow { Group: var group }:
                    outerRow++;
                    if (groups.ContainsKey(group.Id))
                    {
                        throw new DashSpecParseException(
                            $"{context}: duplicate layout group id '{group.Id}'.");
                    }

                    var innerBoard = LayoutBoardDefinition.FromCardRows(group.Rows);
                    var inner = LayoutBoardPlacer.Resolve(
                        innerBoard,
                        columns,
                        $"{context} group '{group.Id}'",
                        token => CardLayoutRefResolver.Resolve(token, tabCards, context));

                    groups[group.Id] = new LayoutGroupPlacement(
                        group.Id,
                        DisplayResolution.TryResolveLayoutGroupChromeTitle(group),
                        outerRow,
                        inner);
                    foreach (var (cardId, placement) in inner)
                    {
                        allCards[cardId] = placement;
                    }

                    break;
            }
        }

        return new TabLayoutPlan(board.Entries, topLevel, groups, nests, allCards);
    }

    private static Dictionary<string, LayoutBoardNestDefinition> CollectNests(
        LayoutBoardDefinition board,
        string context)
    {
        var nests = new Dictionary<string, LayoutBoardNestDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in board.Entries)
        {
            if (entry is not LayoutBoardNestRow { Nest: var nest })
            {
                continue;
            }

            if (nest.Rows.Count == 0)
            {
                throw new DashSpecParseException(
                    $"{context}: nest '{nest.Id}' requires at least one row [ … ].");
            }

            if (!nests.TryAdd(nest.Id, nest))
            {
                throw new DashSpecParseException(
                    $"{context}: duplicate layout nest id '{nest.Id}'.");
            }
        }

        return nests;
    }

    private static void PlaceCardRow(
        IReadOnlyList<string> row,
        int gridRow,
        int columns,
        string context,
        IReadOnlyList<CardDefinition> tabCards,
        IReadOnlyDictionary<string, LayoutBoardNestDefinition> nestDefinitions,
        IDictionary<string, PlacementDefinition> topLevel,
        IDictionary<string, PlacementDefinition> allCards,
        IDictionary<string, LayoutNestPlacement> nests)
    {
        var rowCards = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase);
        var rowNests = new Dictionary<string, LayoutNestPlacement>(StringComparer.OrdinalIgnoreCase);

        LayoutBoardRowPlacer.PlaceRow(
            row,
            gridRow,
            columns,
            context,
            token => LayoutBoardRefResolver.Resolve(token, tabCards, nestDefinitions, context),
            (target, placement, cards, nestPlacements, nestDefs) =>
            {
                switch (target.Kind)
                {
                    case LayoutBoardRefKind.Card:
                        if (!cards.TryAdd(target.Id, placement))
                        {
                            throw new DashSpecParseException(
                                $"{context}: '{target.Id}' appears more than once in the layout board.");
                        }

                        break;
                    case LayoutBoardRefKind.Nest:
                        if (!nestDefs.TryGetValue(target.Id, out var nestDef))
                        {
                            throw new DashSpecParseException(
                                $"{context}: nest '{target.Id}' is not declared.");
                        }

                        if (nestPlacements.ContainsKey(target.Id))
                        {
                            throw new DashSpecParseException(
                                $"{context}: nest '{target.Id}' appears more than once in the layout board.");
                        }

                        var innerBoard = LayoutBoardDefinition.FromCardRows(nestDef.Rows);
                        var inner = LayoutBoardPlacer.Resolve(
                            innerBoard,
                            columns,
                            $"{context} nest '{target.Id}'",
                            token => CardLayoutRefResolver.Resolve(token, tabCards, context));
                        foreach (var (cardId, innerPlacement) in inner)
                        {
                            if (!allCards.TryAdd(cardId, innerPlacement))
                            {
                                throw new DashSpecParseException(
                                    $"{context}: '{cardId}' appears more than once in the layout board.");
                            }
                        }

                        nestPlacements[target.Id] = new LayoutNestPlacement(
                            target.Id,
                            placement,
                            nestDef.Rows,
                            inner);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown layout ref kind '{target.Kind}'.");
                }
            },
            rowCards,
            rowNests,
            nestDefinitions);

        foreach (var (cardId, placement) in rowCards)
        {
            if (!topLevel.TryAdd(cardId, placement))
            {
                throw new DashSpecParseException(
                    $"{context}: '{cardId}' appears more than once in the layout board.");
            }

            allCards[cardId] = placement;
        }

        foreach (var (nestId, nestPlacement) in rowNests)
        {
            nests[nestId] = nestPlacement;
        }
    }
}
