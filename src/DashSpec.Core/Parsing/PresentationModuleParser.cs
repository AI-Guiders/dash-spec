using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class PresentationModuleParser
{
    public static PresentationBlock ParsePresentationFile(string text) =>
        ParsePresentationFile(text, null);

    public static (string Id, PresentationBlock Block) ParsePresentationFileWithId(string text, string? baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var reader = ParserUtilities.CreateReader(text);
        reader.SkipFileDirectives();
        reader.Expect(TokenKind.At);
        reader.ExpectKeyword("presentation");
        var id = reader.ReadIdent();
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new DashSpecParseException("@presentation module requires @presentation <id>.");
        }

        reader.SkipNewlines();
        return (id, ParsePresentationBody(reader, baseDirectory));
    }

    public static PresentationBlock ParsePresentationFile(string text, string? baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var reader = ParserUtilities.CreateReader(text);
        reader.SkipFileDirectives();
        reader.Expect(TokenKind.At);
        reader.ExpectKeyword("presentation");
        _ = reader.ReadIdent();
        reader.SkipNewlines();

        return ParsePresentationBody(reader, baseDirectory);
    }

    private static PresentationBlock ParsePresentationBody(TokenReader reader, string? baseDirectory)
    {
        PresentationBlock? merged = null;
        while (!reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.IsEof)
            {
                break;
            }

            if (!reader.TryKeyword("include"))
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                throw new DashSpecParseException(
                    "Presentation include requires a base directory (parse from file path).");
            }

            var (kind, reference) = DiagramModuleParser.ReadIncludeReference(reader);
            if (!IsChartChromeIncludeKind(kind))
            {
                throw new DashSpecParseException(
                    $"@presentation module only supports include presentation/chrome, got '{kind}'.");
            }

            var fragment = SpecIncludeResolver.Load(kind, reference, baseDirectory);
            merged = SpecIncludeResolver.Merge(
                new SpecIncludeFragment(null, merged, null),
                fragment).Presentation;
            reader.SkipNewlines();
        }

        Dictionary<string, string> props;
        if (reader.IsEof)
        {
            props = [];
        }
        else if (reader.TryKeyword("presentation"))
        {
            props = PropertyBlockParser.Parse(reader, PropertySchemas.Presentation, "presentation");
        }
        else
        {
            props = PropertyBlockParser.ParseFlatProperties(
                reader,
                PropertySchemas.Presentation,
                "@presentation module");
        }

        if (props.Count == 0 && merged is null)
        {
            throw new DashSpecParseException("@presentation module requires at least one property.");
        }

        props.TryGetValue("use", out var usePreset);
        var inline = props
            .Where(x => !string.Equals(x.Key, "use", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        var local = props.Count > 0 || usePreset is not null
            ? new PresentationBlock(usePreset, inline)
            : null;
        var result = SpecIncludeResolver.Merge(
            new SpecIncludeFragment(null, merged, null),
            new SpecIncludeFragment(null, local, null)).Presentation;

        return result ?? throw new DashSpecParseException("@presentation module requires at least one property.");
    }

    internal static bool IsChartChromeIncludeKind(string kind) =>
        string.Equals(kind, "presentation", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(kind, "chrome", StringComparison.OrdinalIgnoreCase);
}
