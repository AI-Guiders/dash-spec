using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class DashboardParser
{
    public static string? ReadRuntimePath(string text) =>
        DocumentModuleParser.ReadRuntimeManifest(text);

    [Obsolete("Use ReadRuntimePath. @config is a deprecated alias for @runtime.")]
    public static string? ReadConfigPath(string text) => ReadRuntimePath(text);

    public static string? ReadDiagramLibraryPath(string text) =>
        DocumentModuleParser.ReadConfigurationValue(text, "diagramlibrary");

    public static string? ReadPalettePath(string text) =>
        DocumentModuleParser.ReadConfigurationValue(text, "palette");

    public static SqlDialect ReadSqlDialect(string text)
    {
        var dialect = DocumentModuleParser.ReadConfigurationValue(text, "sqldialect");
        return dialect is null ? SqlDialect.TSql : SqlDialectParser.Parse(dialect);
    }

    public static (string Id, string Title) ReadDashboardHeader(string text)
    {
        if (!DocumentModuleParser.IsBlockModuleFormat(text))
        {
            throw new DashSpecParseException("ReadDashboardHeader requires block module format.");
        }

        return ReadBlockDashboardHeader(text);
    }

    internal static string ReadPaletteReference(TokenReader reader)
    {
        if (reader.RawKind is TokenKind.Eq)
        {
            reader.Advance();
        }

        return reader.ReadScalarValue();
    }

    private static (string Id, string Title) ReadBlockDashboardHeader(string text)
    {
        var reader = ParserUtilities.CreateReader(text);
        reader.SkipNewlines();
        reader.Expect(TokenKind.At);
        if (reader.TryKeyword("tab"))
        {
            var id = reader.ReadIdent();
            return (id, id);
        }

        reader.ExpectKeyword("dashboard");
        var dashboardId = reader.ReadIdent();
        reader.SkipNewlines();
        reader.Expect(TokenKind.LBrace);
        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.TryKeyword("report"))
            {
                var title = reader.CurrentKind is TokenKind.String
                    ? reader.ReadString()
                    : dashboardId;
                return (dashboardId, title);
            }

            SkipEnvelopeSection(reader);
        }

        return (dashboardId, dashboardId);
    }

    private static void SkipEnvelopeSection(TokenReader reader)
    {
        if (reader.TryKeyword("runtime") ||
            reader.TryKeyword("configuration") ||
            reader.TryKeyword("wiring"))
        {
            SkipBlock(reader);
            return;
        }

        if (reader.TryModuleInclude(out _))
        {
            return;
        }

        throw reader.Unexpected();
    }

    private static void SkipBlock(TokenReader reader)
    {
        reader.Expect(TokenKind.LBrace);
        var depth = 1;
        while (depth > 0 && !reader.IsEof)
        {
            if (reader.IsAt(TokenKind.LBrace))
            {
                reader.Advance();
                depth++;
                continue;
            }

            if (reader.IsAt(TokenKind.RBrace))
            {
                reader.Advance();
                depth--;
                continue;
            }

            reader.Advance();
        }
    }
}
