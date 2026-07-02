using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class DashboardParser
{
    public static string? ReadRuntimePath(string text) =>
        ReadDirective(text, reader => reader.ConsumedRuntimePath);

    [Obsolete("Use ReadRuntimePath. @config is a deprecated alias for @runtime.")]
    public static string? ReadConfigPath(string text) => ReadRuntimePath(text);

    public static string? ReadDiagramLibraryPath(string text) =>
        ReadDirective(text, reader => reader.ConsumedDiagramLibraryPath);

    public static string? ReadPalettePath(string text) =>
        ReadDirective(text, reader => reader.ConsumedPalettePath);

    public static SqlDialect ReadSqlDialect(string text) =>
        ReadDirective(text, reader => reader.ConsumedSqlDialect);

    public static (string Id, string Title) ReadDashboardHeader(string text)
    {
        if (DashboardComposer.IsTabRootDocument(text))
        {
            var reader = ParserUtilities.CreateReader(text);
            reader.SkipFileDirectives();
            reader.Expect(TokenKind.At);
            reader.ExpectKeyword("tab");
            var id = reader.ReadIdent();
            return (id, id);
        }

        var dashboardReader = ParserUtilities.CreateReader(text);
        dashboardReader.SkipFileDirectives();
        dashboardReader.Expect(TokenKind.At);
        dashboardReader.ExpectKeyword("dashboard");
        var dashboardId = dashboardReader.ReadIdent();
        dashboardReader.SkipNewlines();
        dashboardReader.ExpectKeyword("dashboard");
        var title = dashboardReader.ReadString();
        return (dashboardId, title);
    }

    public static DashboardDocument ParseDashboard(string text, string? specDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var reader = ParserUtilities.CreateReader(text);
        reader.SkipFileDirectives();
        var sqlDialect = reader.ConsumedSqlDialect;
        var diagramLibraryPath = reader.ConsumedDiagramLibraryPath;
        var palettePath = reader.ConsumedPalettePath;

        reader.Expect(TokenKind.At);
        reader.ExpectKeyword("dashboard");
        var id = reader.ReadIdent();
        reader.SkipNewlines();

        reader.ExpectKeyword("dashboard");
        var title = reader.ReadString();
        reader.Expect(TokenKind.LBrace);
        reader.SkipNewlines();

        var shell = new DashboardShellContext
        {
            Mode = DashboardShellMode.DashboardBody,
            SpecDirectory = specDirectory,
        };

        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            if (!DashboardShellParser.TryParseStatement(reader, shell))
            {
                throw reader.Unexpected();
            }
        }

        reader.Expect(TokenKind.RBrace);

        var cards = TabParser.AssignTabs(shell.Cards, shell.Tabs);
        var dashboardFilters = ToolbarPlacementResolver.ResolveFilterNames(
            shell.Filters,
            shell.DashboardFilters,
            shell.ToolbarBoard);
        return new DashboardDocument(
            id,
            title,
            shell.ConnectorId,
            sqlDialect,
            diagramLibraryPath,
            palettePath,
            shell.ColorPalette,
            shell.Layout,
            shell.FiltersChrome,
            shell.Filters,
            dashboardFilters,
            shell.Tabs,
            cards,
            shell.ToolbarBoard);
    }

    private static T ReadDirective<T>(string text, Func<TokenReader, T> select)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var reader = ParserUtilities.CreateReader(text);
        reader.SkipFileDirectives();
        return select(reader);
    }

    internal static string ReadPaletteReference(TokenReader reader)
    {
        if (reader.RawKind is TokenKind.Eq)
        {
            reader.Advance();
        }

        return reader.ReadScalarValue();
    }
}
