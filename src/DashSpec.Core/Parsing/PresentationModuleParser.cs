using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class PresentationModuleParser
{
    public static PresentationBlock ParsePresentationFile(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var reader = ParserUtilities.CreateReader(text);
        reader.SkipFileDirectives();
        reader.Expect(TokenKind.At);
        reader.ExpectKeyword("presentation");
        _ = reader.ReadIdent();
        reader.SkipNewlines();

        Dictionary<string, string> props;
        if (reader.TryKeyword("presentation"))
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

        if (props.Count == 0)
        {
            throw new DashSpecParseException("@presentation module requires at least one property.");
        }

        props.TryGetValue("use", out var usePreset);
        var inline = props
            .Where(x => !string.Equals(x.Key, "use", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        return new PresentationBlock(usePreset, inline);
    }
}
