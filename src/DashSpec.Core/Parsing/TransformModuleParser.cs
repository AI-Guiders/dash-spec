using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class TransformModuleParser
{
    public static SeriesTransformBlock ParseTransformFile(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var reader = ParserUtilities.CreateReader(text);
        reader.SkipFileDirectives();
        reader.Expect(TokenKind.At);
        reader.ExpectKeyword("transform");
        _ = reader.ReadIdent();
        reader.SkipNewlines();

        Dictionary<string, string> props;
        if (reader.TryKeyword("transform"))
        {
            if (!reader.TryKeyword("series"))
            {
                throw new DashSpecParseException("Expected 'series' after transform.");
            }

            props = PropertyBlockParser.Parse(reader, PropertySchemas.SeriesTransform, "transform series");
        }
        else
        {
            props = PropertyBlockParser.ParseFlatProperties(
                reader,
                PropertySchemas.SeriesTransform,
                "@transform module");
        }

        if (props.Count == 0)
        {
            throw new DashSpecParseException("@transform module requires at least one property.");
        }

        props.TryGetValue("use", out var usePreset);
        int? max = null;
        if (props.TryGetValue("max", out var rawMax) &&
            int.TryParse(rawMax, out var parsedMax) &&
            parsedMax > 0)
        {
            max = parsedMax;
        }

        props.TryGetValue("other", out var other);
        return new SeriesTransformBlock(usePreset, max, other);
    }
}
