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

        if (!reader.IsOnNewline())
        {
            throw new DashSpecParseException(
                $"Tab '{id}' requires dashspec \"path\" or a body closed with end tab.");
        }

        BlockSyntax.BeginBlock(reader);
        reader.SkipNewlines();

        IReadOnlyList<string> cardIds = [];
        LayoutBoardDefinition? layoutBoard = null;

        while (!BlockSyntax.IsBlockEnd(reader, "tab", id) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, "tab", id))
            {
                break;
            }

            if (reader.TryKeyword("cards"))
            {
                cardIds = PropertyBlockParser.ParseIdentListBlock(reader, "cards", $"tab {id} cards");
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

        BlockSyntax.ExpectBlockEnd(reader, "tab", id);

        if (cardIds.Count == 0)
        {
            throw new DashSpecParseException($"Tab '{id}' requires a cards block or dashspec \"path\".");
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
        if (reader.IsOnNewline())
        {
            reader.SkipNewlines();
        }

        if (reader.IsEof || BlockSyntax.IsBlockEnd(reader, "tab", expectedTabId))
        {
            return (label, filters, layoutBoard);
        }

        BlockSyntax.BeginBlock(reader);
        reader.SkipNewlines();

        while (!BlockSyntax.IsBlockEnd(reader, "tab", expectedTabId) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, "tab", expectedTabId))
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
                $"Tab module '{expectedTabId}' allows only layout and filter declarations in tab block, not '{key}'.");
        }

        BlockSyntax.ExpectBlockEnd(reader, "tab", expectedTabId);
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
