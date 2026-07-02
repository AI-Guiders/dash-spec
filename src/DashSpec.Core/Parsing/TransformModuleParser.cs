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
        reader.ExpectKeyword("transform");
        reader.ExpectKeyword("series");

        var props = PropertyBlockParser.Parse(reader, PropertySchemas.SeriesTransform, "transform series");
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
