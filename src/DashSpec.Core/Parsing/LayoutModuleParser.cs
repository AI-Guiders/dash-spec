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
        return LayoutParser.ParseBoardRows(reader);
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
