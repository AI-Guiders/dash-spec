using DashSpec.Core.Analysis;
using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class DashboardParser
{
    public static string? ReadConfigPath(string text) => ReadDirective(text, reader => reader.ConsumedConfigPath);

    public static string? ReadDiagramLibraryPath(string text) =>
        ReadDirective(text, reader => reader.ConsumedDiagramLibraryPath);

    public static SqlDialect ReadSqlDialect(string text) =>
        ReadDirective(text, reader => reader.ConsumedSqlDialect);

    public static (string Id, string Title) ReadDashboardHeader(string text)
    {
        var reader = CreateReader(text);
        reader.SkipFileDirectives();
        reader.Expect(TokenKind.At);
        reader.ExpectKeyword("dashboard");
        var id = reader.ReadIdent();
        reader.SkipNewlines();
        reader.ExpectKeyword("dashboard");
        var title = reader.ReadString();
        return (id, title);
    }

    public static DashboardDocument Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var reader = CreateReader(text);
        reader.SkipFileDirectives();
        var sqlDialect = reader.ConsumedSqlDialect;
        var diagramLibraryPath = reader.ConsumedDiagramLibraryPath;

        reader.Expect(TokenKind.At);
        reader.ExpectKeyword("dashboard");
        var id = reader.ReadIdent();
        reader.SkipNewlines();

        reader.ExpectKeyword("dashboard");
        var title = reader.ReadString();
        reader.Expect(TokenKind.LBrace);
        reader.SkipNewlines();

        var filters = new List<FilterDefinition>();
        var dashboardFilters = new List<string>();
        var tabs = new List<TabDefinition>();
        var cards = new List<CardDefinition>();
        string? connectorId = null;
        LayoutDefinition layout = LayoutDefinition.Default;
        FiltersChromeDefinition filtersChrome = FiltersChromeDefinition.Default;

        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
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
                tabs.Add(TabParser.Parse(reader));
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

        reader.Expect(TokenKind.RBrace);

        cards = TabParser.AssignTabs(cards, tabs);
        var document = new DashboardDocument(
            id,
            title,
            connectorId,
            sqlDialect,
            diagramLibraryPath,
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

    private static T ReadDirective<T>(string text, Func<TokenReader, T> select)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var reader = CreateReader(text);
        reader.SkipFileDirectives();
        return select(reader);
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
