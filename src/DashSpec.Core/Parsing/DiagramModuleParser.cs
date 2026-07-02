using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class DiagramModuleParser
{
    public static SpecIncludeFragment ParseDiagramFile(string text, string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var reader = ParserUtilities.CreateReader(text);
        reader.SkipFileDirectives();
        reader.Expect(TokenKind.At);
        reader.ExpectKeyword("diagram");
        _ = reader.ReadIdent();
        reader.SkipNewlines();

        return ParseFragmentBody(reader, baseDirectory);
    }

    internal static SpecIncludeFragment ParseFragmentBody(TokenReader reader, string baseDirectory)
    {
        var fragment = new SpecIncludeFragment(null, null, null);

        while (!reader.IsEof && !reader.IsAt(TokenKind.RBrace))
        {
            if (reader.TryKeyword("include"))
            {
                var (kind, reference) = ReadIncludeReference(reader);
                fragment = SpecIncludeResolver.Merge(fragment, SpecIncludeResolver.Load(kind, reference, baseDirectory));
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("diagram"))
            {
                fragment = SpecIncludeResolver.Merge(
                    fragment,
                    new SpecIncludeFragment(DiagramParser.Parse(reader), null, null));
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("presentation"))
            {
                fragment = SpecIncludeResolver.Merge(
                    fragment,
                    new SpecIncludeFragment(null, ParsePresentation(reader), null));
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("transform"))
            {
                if (!reader.TryKeyword("series"))
                {
                    throw new DashSpecParseException("Expected 'series' after transform.");
                }

                fragment = SpecIncludeResolver.Merge(
                    fragment,
                    new SpecIncludeFragment(null, null, ParseSeriesTransform(reader)));
                reader.SkipNewlines();
                continue;
            }

            if (reader.IsAt(TokenKind.RBrace))
            {
                break;
            }

            throw reader.Unexpected();
        }

        if (fragment.Diagram is null)
        {
            throw new DashSpecParseException("Diagram module requires a diagram { } block.");
        }

        return fragment;
    }

    internal static (string Kind, string Reference) ReadIncludeReference(TokenReader reader)
    {
        var kind = reader.ReadIdent();
        var reference = reader.ReadString();
        return (kind, reference);
    }

    private static PresentationBlock ParsePresentation(TokenReader reader)
    {
        var props = PropertyBlockParser.Parse(reader, PropertySchemas.Presentation, "presentation");
        props.TryGetValue("use", out var usePreset);
        var inline = props
            .Where(x => !string.Equals(x.Key, "use", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        return new PresentationBlock(usePreset, inline);
    }

    private static SeriesTransformBlock ParseSeriesTransform(TokenReader reader)
    {
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
