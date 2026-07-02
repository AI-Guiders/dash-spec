using DashSpec.Core.Layout;
using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class TabParser
{
    public static TabDefinition Parse(TokenReader reader)
    {
        var id = reader.ReadIdent();
        string? label = null;
        if (reader.TryKeyword("as"))
        {
            label = reader.ReadString();
        }

        if (reader.TryKeyword("dashspec"))
        {
            var path = reader.ReadString();
            return new TabDefinition(id, label, [], path);
        }

        reader.Expect(TokenKind.LBrace);
        reader.SkipNewlines();

        IReadOnlyList<string> cardIds = [];
        LayoutBoardDefinition? layoutBoard = null;

        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.IsAt(TokenKind.RBrace))
            {
                break;
            }

            if (reader.TryKeyword("cards"))
            {
                cardIds = PropertyBlockParser.ParseIdentListBlock(reader, $"tab {id} cards");
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("layout"))
            {
                layoutBoard = LayoutParser.ParseBoard(reader);
                reader.SkipNewlines();
                continue;
            }

            var key = reader.ReadIdent();
            throw new DashSpecParseException($"Unknown property '{key}' in tab {id} block.");
        }

        reader.Expect(TokenKind.RBrace);

        if (cardIds.Count == 0)
        {
            throw new DashSpecParseException($"Tab '{id}' requires a cards {{ }} block or dashspec \"path\".");
        }

        return new TabDefinition(id, label, cardIds, LayoutBoard: layoutBoard);
    }

    /// <summary>Tab block inside @tab module: optional label, layout board, tab-local filters.</summary>
    public static (string? Label, IReadOnlyList<FilterDefinition> Filters, LayoutBoardDefinition? LayoutBoard)
        ParseModuleLocalBlock(
        TokenReader reader,
        string expectedTabId,
        bool allowFilters)
    {
        var id = reader.ReadIdent();
        if (!string.Equals(id, expectedTabId, StringComparison.OrdinalIgnoreCase))
        {
            throw new DashSpecParseException(
                $"Tab module declares @tab '{expectedTabId}' but tab block uses '{id}'.");
        }

        string? label = null;
        if (reader.TryKeyword("as"))
        {
            label = reader.ReadString();
        }

        var filters = new List<FilterDefinition>();
        LayoutBoardDefinition? layoutBoard = null;
        if (!reader.IsAt(TokenKind.LBrace))
        {
            return (label, filters, layoutBoard);
        }

        reader.Expect(TokenKind.LBrace);
        reader.SkipNewlines();

        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.IsAt(TokenKind.RBrace))
            {
                break;
            }

            if (reader.TryKeyword("layout"))
            {
                layoutBoard = LayoutParser.ParseBoard(reader);
                reader.SkipNewlines();
                continue;
            }

            if (allowFilters && reader.TryKeyword("filter"))
            {
                filters.Add(FilterParser.Parse(reader));
                reader.SkipNewlines();
                continue;
            }

            var key = reader.ReadIdent();
            throw new DashSpecParseException(
                $"Tab module '{expectedTabId}' allows only layout and filter declarations in tab {{ }}, not '{key}'.");
        }

        reader.Expect(TokenKind.RBrace);
        return (label, filters, layoutBoard);
    }

    public static List<CardDefinition> AssignTabs(
        IReadOnlyList<CardDefinition> cards,
        IReadOnlyList<TabDefinition> tabs)
    {
        if (tabs.Count == 0)
        {
            return cards.ToList();
        }

        var idToTab = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tab in tabs)
        {
            foreach (var token in tab.CardIds)
            {
                var cardId = CardLayoutRefResolver.Resolve(
                    token,
                    cards,
                    $"Tab '{tab.Id}' cards");
                idToTab[cardId] = tab.Id;
            }
        }

        return cards
            .Select(card => card with { TabId = idToTab.GetValueOrDefault(card.Id) })
            .ToList();
    }
}
