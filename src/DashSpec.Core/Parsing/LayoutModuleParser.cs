using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class LayoutModuleParser
{
    public static LayoutBoardDefinition ParseLayoutFile(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var reader = ParserUtilities.CreateReader(text);
        reader.SkipFileDirectives();
        reader.Expect(TokenKind.At);
        reader.ExpectKeyword("layout");
        var id = reader.ReadIdent();
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new DashSpecParseException("Layout module requires @layout <id>.");
        }

        reader.SkipNewlines();
        var scope = ParseMandatoryScope(reader);
        reader.SkipNewlines();
        var board = LayoutParser.ParseBoardRows(reader);
        return board with { ModuleScope = scope };
    }

    private static LayoutScope ParseMandatoryScope(TokenReader reader)
    {
        if (!reader.TryKeyword("scope"))
        {
            throw new DashSpecParseException(
                "Layout module requires scope toolbar|tab|card after @layout <id>.");
        }

        var kind = reader.ReadIdent();
        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new DashSpecParseException(
                "Layout module requires scope toolbar|tab|card after @layout <id>.");
        }

        return kind.ToLowerInvariant() switch
        {
            "toolbar" => LayoutScope.Toolbar,
            "tab" => LayoutScope.Tab,
            "card" => LayoutScope.Card,
            _ => throw new DashSpecParseException(
                $"Layout module scope must be toolbar, tab, or card; got '{kind}'."),
        };
    }

    public static LayoutBoardDefinition Load(string reference, string specDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(specDirectory);

        var path = SpecIncludeResolver.ResolvePath(reference, specDirectory);
        path = ResolveLayoutFile(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Include layout not found: '{reference}' (resolved: {path}).",
                path);
        }

        return ParseLayoutFile(File.ReadAllText(path));
    }

    private static string ResolveLayoutFile(string path)
    {
        if (File.Exists(path))
        {
            return path;
        }

        const string extension = ".dashlayout";
        var withExt = path.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? path : path + extension;
        return withExt;
    }
}
