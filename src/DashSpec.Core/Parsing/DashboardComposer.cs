using DashSpec.Core.Analysis;
using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class DashboardComposer
{
    public static DashboardDocument Parse(string text, string? specDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (IsTabRootDocument(text))
        {
            return TabModuleParser.ComposeStandalone(text);
        }

        var document = DashboardParser.ParseDashboard(text);
        if (document.Tabs.All(t => string.IsNullOrWhiteSpace(t.DashspecPath)))
        {
            TabAnalyzer.Validate(document);
            return document;
        }

        if (string.IsNullOrWhiteSpace(specDirectory))
        {
            throw new DashSpecParseException(
                "Tab dashspec references require specDirectory when parsing (path to the root .dashspec file directory).");
        }

        return MergeTabModules(document, specDirectory);
    }

    public static bool IsTabRootDocument(string text)
    {
        var reader = CreateReader(text);
        reader.SkipFileDirectives();
        reader.SkipNewlines();
        if (!reader.IsAt(TokenKind.At))
        {
            return false;
        }

        reader.Advance();
        return reader.TryKeyword("tab");
    }

    private static DashboardDocument MergeTabModules(DashboardDocument document, string specDirectory)
    {
        var filters = document.Filters.ToList();
        var cards = document.Cards.ToList();
        var mergedTabs = new List<TabDefinition>();

        foreach (var tab in document.Tabs)
        {
            if (string.IsNullOrWhiteSpace(tab.DashspecPath))
            {
                mergedTabs.Add(tab);
                continue;
            }

            var modulePath = Path.GetFullPath(Path.Combine(specDirectory, tab.DashspecPath));
            if (!File.Exists(modulePath))
            {
                throw new FileNotFoundException(
                    $"Tab '{tab.Id}' dashspec not found: '{tab.DashspecPath}' (resolved: {modulePath}).",
                    modulePath);
            }

            var module = TabModuleParser.ParseEmbedded(File.ReadAllText(modulePath), tab.Id);
            foreach (var filter in module.Filters)
            {
                if (filters.Any(f => string.Equals(f.Name, filter.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new DashSpecParseException(
                        $"Tab module '{tab.Id}' redeclares filter '{filter.Name}' already on parent dashboard.");
                }

                filters.Add(filter);
            }

            foreach (var card in module.Cards)
            {
                if (cards.Any(c => string.Equals(c.Id, card.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new DashSpecParseException(
                        $"Tab module '{tab.Id}' redeclares card '{card.Id}' already on parent dashboard.");
                }

                cards.Add(card);
            }

            var label = tab.Label ?? module.Label;
            mergedTabs.Add(new TabDefinition(
                tab.Id,
                label,
                module.Cards.Select(c => c.Id).ToList()));
        }

        cards = TabParser.AssignTabs(cards, mergedTabs);
        var merged = document with
        {
            Filters = filters,
            Cards = cards,
            Tabs = mergedTabs,
        };

        FilterPlacementAnalyzer.Validate(merged);
        TabAnalyzer.Validate(merged);
        return merged;
    }

    private static TokenReader CreateReader(string text)
    {
        var tokens = DashSpecLexer.Tokenize(text);
        return new TokenReader(tokens);
    }
}
