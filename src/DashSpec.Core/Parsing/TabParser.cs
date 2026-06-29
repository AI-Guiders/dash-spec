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

            var key = reader.ReadIdent();
            throw new DashSpecParseException($"Unknown property '{key}' in tab {id} block.");
        }

        reader.Expect(TokenKind.RBrace);

        if (cardIds.Count == 0)
        {
            throw new DashSpecParseException($"Tab '{id}' requires a cards {{ }} block or dashspec \"path\".");
        }

        return new TabDefinition(id, label, cardIds);
    }

    /// <summary>Tab block inside @tab module: optional label + tab-local filters only.</summary>
    public static (string? Label, IReadOnlyList<FilterDefinition> Filters) ParseModuleLocalBlock(
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
        if (!reader.IsAt(TokenKind.LBrace))
        {
            return (label, filters);
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

            if (allowFilters && reader.TryKeyword("filter"))
            {
                filters.Add(FilterParser.Parse(reader));
                reader.SkipNewlines();
                continue;
            }

            var key = reader.ReadIdent();
            throw new DashSpecParseException(
                $"Tab module '{expectedTabId}' allows only filter declarations in tab {{ }}, not '{key}'.");
        }

        reader.Expect(TokenKind.RBrace);
        return (label, filters);
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
            foreach (var cardId in tab.CardIds)
            {
                idToTab[cardId] = tab.Id;
            }
        }

        return cards
            .Select(card => card with { TabId = idToTab.GetValueOrDefault(card.Id) })
            .ToList();
    }
}

internal static class FiltersChromeParser
{
    public static FiltersChromeDefinition Parse(TokenReader reader)
    {
        var props = PropertyBlockParser.Parse(reader, PropertySchemas.FiltersChrome, "filters chrome");

        var layout = "card";
        var sticky = FiltersChromeDefinition.StickyNone;
        var apply = "manual";
        var debounceMs = 400;

        if (props.TryGetValue("layout", out var layoutRaw))
        {
            layout = layoutRaw.ToLowerInvariant() switch
            {
                "card" or "bar" => layoutRaw.ToLowerInvariant(),
                _ => throw new DashSpecParseException("filters chrome layout must be 'card' or 'bar'."),
            };
        }

        if (props.TryGetValue("sticky", out var stickyRaw))
        {
            sticky = FiltersChromeStickyParser.Parse(stickyRaw);
        }

        if (props.TryGetValue("apply", out var applyRaw))
        {
            apply = applyRaw.ToLowerInvariant() switch
            {
                "manual" or "auto" => applyRaw.ToLowerInvariant(),
                _ => throw new DashSpecParseException("filters chrome apply must be 'manual' or 'auto'."),
            };
        }

        if (props.TryGetValue("debounce_ms", out var debounceRaw) &&
            int.TryParse(debounceRaw, out var parsedDebounce) &&
            parsedDebounce >= 0)
        {
            debounceMs = parsedDebounce;
        }

        return new FiltersChromeDefinition(layout, sticky, apply, debounceMs);
    }
}
