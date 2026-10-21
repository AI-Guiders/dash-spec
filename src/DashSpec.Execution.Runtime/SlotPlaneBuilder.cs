using DashSpec.Modeling.Core;
using DashSpec.Modeling.Parse.Layout;

namespace DashSpec.Execution.Runtime;

/// <summary>
/// Assembles the <see cref="SlotPlane"/> ([ADR-0111]): the correspondence — compiled thing ↔
/// its visual entry — the same relation by which things were added to the document at compile,
/// only the visual layer. Every input is an existing planner/compactor function the render path
/// calls today; the invocation moves here — once per document revision, at the report bootstrap.
/// The render then reads the assembled model and places ([ADR-0111] §4).
/// </summary>
public static class SlotPlaneBuilder
{
    public static SlotPlane Build(
        DashboardDocument document,
        SpecLibrary? library = null,
        string? entryId = null,
        string? initialTabId = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var nodes = document.WiringGraph.Nodes;
        var boards = new Dictionary<BoardRoute, IReadOnlyList<Slot>>(BoardRouteComparer.Instance);
        var toolbars = new Dictionary<BoardRoute, IReadOnlyList<Slot>>(BoardRouteComparer.Instance);

        if (document.Tabs.Count == 0)
        {
            // Tabless document: one implicit route; the fallback planning over all cards.
            var route = new BoardRoute { TabId = null, PageId = null };
            boards[route] = PlanTablessBoard(document, library, nodes);
            toolbars[route] = PlanToolbar(document, tabId: null, pageId: null, nodes);
        }
        else
        {
            foreach (var tab in document.Tabs)
            {
                var pages = PageTabScope.FilterForTab(document.Pages?.Value, tab.Id);
                if (pages.Count == 0)
                {
                    var route = new BoardRoute { TabId = Some(tab.Id), PageId = null };
                    boards[route] = PlanBoard(document, library, tab.Id, pageId: null, nodes);
                    toolbars[route] = PlanToolbar(document, tab.Id, pageId: null, nodes);
                    continue;
                }

                foreach (var page in pages)
                {
                    var route = new BoardRoute { TabId = Some(tab.Id), PageId = Some(page.Id) };
                    boards[route] = PlanBoard(document, library, tab.Id, page.Id, nodes);
                    toolbars[route] = PlanToolbar(document, tab.Id, page.Id, nodes);
                }
            }
        }

        var interiors = new Dictionary<string, IReadOnlyList<Slot>>(StringComparer.OrdinalIgnoreCase);
        var chrome = new Dictionary<string, IReadOnlyList<Slot>>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in document.Cards)
        {
            interiors[card.Id] = PlanInterior(document, card, nodes);
            chrome[card.Id] = PlanChrome(document, card, nodes);
        }

        return new SlotPlane
        {
            Boards = boards,
            Toolbars = toolbars,
            CardInteriors = interiors,
            CardFilterChrome = chrome,
            InitialRoute = PlanInitialRoute(document, entryId, initialTabId),
        };
    }

    /// <summary>Effective board per route: the page board overrides the tab board ([ADR-0111] §1).</summary>
    static IReadOnlyList<Slot> PlanBoard(
        DashboardDocument document,
        SpecLibrary? library,
        string tabId,
        string? pageId,
        IReadOnlyDictionary<string, WiringNode> nodes)
    {
        var columns = document.Layout.Columns;
        var context = TabLayoutCompactor.ResolveContext(document, tabId, pageId);
        var compact = TabLayoutCompactor.Compact(document, tabId, library, pageId);
        var declared = new HashSet<string>(compact.Keys, StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<LayoutSlotDescriptor> descriptors;
        if (TabLayoutCompactor.TryBuildLayoutPlan(context) is { } plan)
        {
            descriptors = LayoutSlotEngine.PlanHostTabBoard(
                plan,
                columns,
                token => TabLayoutCompactor.ResolveCardRef(context, token));
        }
        else
        {
            var cards = context.TabCards
                .Select(card => (card.Id, ResolveDataFamily(card, library, document)))
                .ToList();
            descriptors = LayoutSlotEngine.PlanHostTabBoardCards(cards, compact, columns);
        }

        return descriptors
            .Select(descriptor => ToSlot(
                descriptor,
                nodes,
                provenance: declared.Contains(descriptor.Token)
                    ? PlacementProvenance.Declared
                    : PlacementProvenance.RecordedDefault))
            .ToList();
    }

    /// <summary>Tabless document: the fallback planning — family defaults recorded ([ADR-0111]).</summary>
    static IReadOnlyList<Slot> PlanTablessBoard(
        DashboardDocument document,
        SpecLibrary? library,
        IReadOnlyDictionary<string, WiringNode> nodes)
    {
        var columns = document.Layout.Columns;
        var cards = document.Cards
            .Select(card => (card.Id, ResolveDataFamily(card, library, document)))
            .ToList();
        var declared = document.Cards
            .Where(card => card.Placement is not null)
            .Select(card => card.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var descriptors = LayoutSlotEngine.PlanHostTabBoardCards(
            cards,
            new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase),
            columns);

        return descriptors
            .Select(descriptor => ToSlot(
                descriptor,
                nodes,
                provenance: declared.Contains(descriptor.Token)
                    ? PlacementProvenance.Declared
                    : PlacementProvenance.RecordedDefault))
            .ToList();
    }

    /// <summary>
    /// Toolbar per route: the page toolbar board when declared, the document-level toolbar otherwise;
    /// unplaced names get the recorded fallback row (the formula from DashboardPageController, [ADR-0111] §4).
    /// </summary>
    static IReadOnlyList<Slot> PlanToolbar(
        DashboardDocument document,
        string? tabId,
        string? pageId,
        IReadOnlyDictionary<string, WiringNode> nodes)
    {
        var columns = document.Layout.Columns;
        List<string> names;
        Dictionary<string, PlacementDefinition> placements;

        var board = PageToolbarResolver.ResolveActiveToolbarBoard(document, tabId, pageId);
        if (board is not null)
        {
            names = board.Rows
                .SelectMany(row => row)
                .Select(cell => LayoutBoardCellParser.parseCell(cell, "Toolbar board", 1).Item1)
                .ToList();
            placements = new Dictionary<string, PlacementDefinition>(
                ToolbarLayoutCompactor.CompactVisible(document, names.ToHashSet(StringComparer.OrdinalIgnoreCase), board),
                StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            names = document.DashboardFilters.ToList();
            placements = new Dictionary<string, PlacementDefinition>(
                ToolbarLayoutCompactor.Compact(document),
                StringComparer.OrdinalIgnoreCase);
        }

        // Recorded fallback row — the unplaced names (the render formula, moved to assembly).
        var unplaced = names.Where(name => !placements.ContainsKey(name)).ToList();
        if (unplaced.Count > 0)
        {
            var row = placements.Count == 0
                ? 1
                : placements.Values.Max(placement => placement.Row) + 1;
            var span = unplaced.Count == 1 ? columns : columns / unplaced.Count;
            for (var i = 0; i < unplaced.Count; i++)
            {
                placements[unplaced[i]] = new PlacementDefinition(row, 1 + i * span, span);
            }
        }

        var recorded = unplaced.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return LayoutSlotEngine.PlanHostPageToolbar(names, placements)
            .Select(descriptor => ToSlot(
                descriptor,
                nodes,
                provenance: recorded.Contains(descriptor.Token)
                    ? PlacementProvenance.RecordedDefault
                    : PlacementProvenance.Declared))
            .ToList();
    }

    /// <summary>Card interior: the declared board or the synthesized default ([ADR-0111]).</summary>
    static IReadOnlyList<Slot> PlanInterior(
        DashboardDocument document,
        CardDefinition card,
        IReadOnlyDictionary<string, WiringNode> nodes)
    {
        var columns = document.Layout.Columns;
        var placements = CardInteriorLayoutCompactor.Compact(card, document.Filters, columns);
        var primarySlotRef = CardDiagramSlotCatalog.ResolvePrimarySlotRef(card);
        var secondaryRefs = CardDiagramSlotCatalog.ResolveSlots(card).Keys
            .Where(slotRef => !string.Equals(slotRef, primarySlotRef, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var declaredTokens = card.InteriorBoard?.Value is { } interiorBoard
            ? InteriorDeclaredTokens(interiorBoard, card)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var synthesized = card.InteriorBoard is null;

        var cardId = card.Id;
        return LayoutSlotEngine.PlanCardInterior(placements, card.LocalFilters, secondaryRefs)
            .Select(descriptor =>
            {
                // The wiring convention for card-scoped slots/filters: bare names — the card
                // scope lives on the edges; the node id is the token as is (no composition).
                var nodeId = descriptor.Token;
                var provenance = synthesized || !declaredTokens.Contains(descriptor.Token)
                    ? PlacementProvenance.RecordedDefault
                    : PlacementProvenance.Declared;
                return ToSlot(descriptor, nodes, provenance, nodeId);
            })
            .ToList();
    }

    /// <summary>Card local filter chrome: the declared board's cells ([ADR-0111]).</summary>
    static IReadOnlyList<Slot> PlanChrome(
        DashboardDocument document,
        CardDefinition card,
        IReadOnlyDictionary<string, WiringNode> nodes)
    {
        var placements = CardLocalFilterChromeCompactor.Compact(card, document.Filters, document.Layout.Columns);
        var recorded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            CardLocalFilterChromeCompactor.ApplySlotId,
        };

        return LayoutSlotEngine.PlanScoped(
                LayoutSlotScope.CardFilterChrome,
                placements,
                token => LayoutSlotContentKind.Filter)
            .Select(descriptor => ToSlot(
                descriptor,
                nodes,
                provenance: recorded.Contains(descriptor.Token)
                    ? PlacementProvenance.RecordedDefault
                    : PlacementProvenance.Declared))
            .ToList();
    }

    static VisualRoute PlanInitialRoute(DashboardDocument document, string? entryId, string? initialTabId)
    {
        // Tabless documents are legitimate (the demo corpus): the implicit route, recorded.
        var tabId = initialTabId is { Length: > 0 }
            ? initialTabId
            : document.Tabs.FirstOrDefault()?.Id ?? "";

        var pageId = tabId.Length == 0
            ? ""
            : PageTabScope
                .FilterForTab(document.Pages?.Value, tabId)
                .FirstOrDefault()
                ?.Id ?? "";

        return new VisualRoute
        {
            EntryId = entryId ?? "",
            TabId = tabId,
            PageId = pageId,
        };
    }

    /// <summary>
    /// The card's diagram family — from the resolved kind (preset cards resolve their kind
    /// through the library, [ADR-0098] composer path; the same call TabLayoutCompactor makes).
    /// </summary>
    static DiagramDataFamily ResolveDataFamily(CardDefinition card, SpecLibrary? library, DashboardDocument document) =>
        DiagramKindRegistry.Resolve(CardResolver.ResolveKind(card, library, document.DashboardFilters)).DataFamily;

    static HashSet<string> InteriorDeclaredTokens(LayoutBoardDefinition board, CardDefinition card)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in board.Rows)
        {
            foreach (var cell in row)
            {
                tokens.Add(LayoutBoardCellParser.parseCell(cell, $"Card '{card.Id}' interior", 1).Item1);
            }
        }

        return tokens;
    }

    static Slot ToSlot(
        LayoutSlotDescriptor descriptor,
        IReadOnlyDictionary<string, WiringNode> nodes,
        PlacementProvenance provenance,
        string? nodeId = null)
    {
        var id = nodeId ?? descriptor.Token;
        if (!nodes.TryGetValue(id, out var node))
        {
            throw new DashSpecParseException(
                $"Slot plane correspondence failed: no document-graph node for '{id}' " +
                $"(scope {descriptor.Scope}, kind {descriptor.ContentKind}).");
        }

        return new Slot
        {
            Node = node,
            Placement = descriptor.Placement,
            Scope = descriptor.Scope,
            Provenance = provenance,
        };
    }

    static Microsoft.FSharp.Core.FSharpOption<string> Some(string value) =>
        Microsoft.FSharp.Core.FSharpOption<string>.Some(value);

    sealed class BoardRouteComparer : IEqualityComparer<BoardRoute>
    {
        public static readonly BoardRouteComparer Instance = new();

        public bool Equals(BoardRoute? left, BoardRoute? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            return string.Equals(OptionId(left.TabId), OptionId(right.TabId), StringComparison.OrdinalIgnoreCase)
                && string.Equals(OptionId(left.PageId), OptionId(right.PageId), StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(BoardRoute route) =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(OptionId(route.TabId))
                ^ StringComparer.OrdinalIgnoreCase.GetHashCode(OptionId(route.PageId));

        static string OptionId(Microsoft.FSharp.Core.FSharpOption<string> option) =>
            option is null ? "" : option.Value ?? "";
    }
}
