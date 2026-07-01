using DashSpec.Core.Analysis;
using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal sealed record TabModuleContent(
    string TabId,
    string? Label,
    IReadOnlyList<FilterDefinition> Filters,
    IReadOnlyList<CardDefinition> Cards);

internal static class TabModuleParser
{
    public static TabModuleContent ParseEmbedded(string text, string expectedTabId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedTabId);

        var reader = CreateReader(text);
        reader.SkipFileDirectives();

        var tabId = ReadTabDirective(reader);
        if (!string.Equals(tabId, expectedTabId, StringComparison.OrdinalIgnoreCase))
        {
            throw new DashSpecParseException(
                $"Tab dashspec for '{expectedTabId}' must declare @tab '{expectedTabId}', found '{tabId}'.");
        }

        string? label = null;
        var filters = new List<FilterDefinition>();
        var cards = new List<CardDefinition>();

        reader.SkipNewlines();
        while (!reader.IsEof)
        {
            if (reader.TryKeyword("connector"))
            {
                reader.ReadIdent();
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("layout"))
            {
                LayoutParser.ParseGrid(reader);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("filters") || reader.TryKeyword("toolbar"))
            {
                if (reader.TryKeyword("dashboard"))
                {
                    _ = ParseFilterPlacementList(reader, "filters dashboard");
                }
                else if (reader.TryKeyword("chrome"))
                {
                    FiltersChromeParser.Parse(reader);
                }
                else
                {
                    _ = ParseFilterPlacementList(reader, "toolbar");
                }

                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("filter"))
            {
                // Standalone shell: top-level filters are for spec_path on module only; parent owns globals when embedded.
                _ = FilterParser.Parse(reader);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("tab"))
            {
                var (moduleLabel, moduleFilters) = TabParser.ParseModuleLocalBlock(reader, tabId, allowFilters: true);
                label ??= moduleLabel;
                filters.AddRange(moduleFilters);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("card"))
            {
                cards.Add(CardParser.Parse(reader, filters));
                reader.SkipNewlines();
                continue;
            }

            if (reader.IsEof)
            {
                break;
            }

            throw reader.Unexpected();
        }

        if (cards.Count == 0)
        {
            throw new DashSpecParseException($"Tab module '{tabId}' must declare at least one card.");
        }

        return new TabModuleContent(tabId, label, filters, cards);
    }

    public static DashboardDocument ComposeStandalone(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var reader = CreateReader(text);
        reader.SkipFileDirectives();
        var sqlDialect = reader.ConsumedSqlDialect;
        var diagramLibraryPath = reader.ConsumedDiagramLibraryPath;

        var tabId = ReadTabDirective(reader);

        var filters = new List<FilterDefinition>();
        var dashboardFilters = new List<string>();
        var cards = new List<CardDefinition>();
        string? connectorId = null;
        LayoutDefinition layout = LayoutDefinition.Default;
        FiltersChromeDefinition filtersChrome = FiltersChromeDefinition.Default;
        string? label = null;

        reader.SkipNewlines();
        while (!reader.IsEof)
        {
            if (reader.TryKeyword("connector"))
            {
                connectorId = reader.ReadIdent();
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("layout"))
            {
                layout = LayoutParser.ParseGrid(reader);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("filters") || reader.TryKeyword("toolbar"))
            {
                if (reader.TryKeyword("dashboard"))
                {
                    dashboardFilters.AddRange(ParseFilterPlacementList(reader, "filters dashboard"));
                }
                else if (reader.TryKeyword("chrome"))
                {
                    filtersChrome = FiltersChromeParser.Parse(reader);
                }
                else
                {
                    dashboardFilters.AddRange(ParseFilterPlacementList(reader, "toolbar"));
                }

                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("filter"))
            {
                filters.Add(FilterParser.Parse(reader));
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("tab"))
            {
                var (moduleLabel, moduleFilters) = TabParser.ParseModuleLocalBlock(reader, tabId, allowFilters: true);
                label ??= moduleLabel;
                filters.AddRange(moduleFilters);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("card"))
            {
                cards.Add(CardParser.Parse(reader, filters));
                reader.SkipNewlines();
                continue;
            }

            throw reader.Unexpected();
        }

        if (cards.Count == 0)
        {
            throw new DashSpecParseException($"Standalone @tab '{tabId}' must declare at least one card.");
        }

        var title = label ?? tabId;
        var tabs = new List<TabDefinition>
        {
            new(tabId, title, cards.Select(c => c.Id).ToList()),
        };

        cards = TabParser.AssignTabs(cards, tabs);
        var document = new DashboardDocument(
            tabId,
            title,
            connectorId,
            sqlDialect,
            diagramLibraryPath,
            null,
            layout,
            filtersChrome,
            filters,
            dashboardFilters,
            tabs,
            cards);

        FilterPlacementAnalyzer.Validate(document);
        TabAnalyzer.Validate(document);
        return document;
    }

    private static string ReadTabDirective(TokenReader reader)
    {
        reader.Expect(TokenKind.At);
        reader.ExpectKeyword("tab");
        return reader.ReadIdent();
    }

    private static TokenReader CreateReader(string text)
    {
        var tokens = DashSpecLexer.Tokenize(text);
        return new TokenReader(tokens);
    }

    private static IReadOnlyList<string> ParseFilterPlacementList(TokenReader reader, string blockName)
    {
        if (reader.IsAt(TokenKind.LBrace))
        {
            return PropertyBlockParser.ParseCommaListBlock(reader, blockName);
        }

        return reader.ReadCommaListInline();
    }
}
