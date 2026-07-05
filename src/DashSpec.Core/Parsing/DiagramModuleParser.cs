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

    public static (string Id, SpecIncludeFragment Fragment) ParseDiagramFileWithId(string text, string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var reader = ParserUtilities.CreateReader(text);
        reader.SkipFileDirectives();
        reader.Expect(TokenKind.At);
        reader.ExpectKeyword("diagram");
        var id = reader.ReadIdent();
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new DashSpecParseException("Diagram module requires @diagram <id>.");
        }

        reader.SkipNewlines();
        return (id, ParseFragmentBody(reader, baseDirectory));
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

            if (TryParseDiagramKindBlock(reader, out var diagram))
            {
                fragment = SpecIncludeResolver.Merge(
                    fragment,
                    new SpecIncludeFragment(diagram, null, null));
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

            if (TryParseSeriesTransform(reader, out var transform))
            {
                fragment = SpecIncludeResolver.Merge(
                    fragment,
                    new SpecIncludeFragment(null, null, transform));
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
            throw new DashSpecParseException("Diagram module requires a chart kind block (e.g. heatmap { }).");
        }

        return fragment;
    }

    private static bool TryParseDiagramKindBlock(TokenReader reader, out DiagramDefinition diagram)
    {
        diagram = null!;
        reader.SkipNewlines();
        if (reader.IsEof || reader.IsAt(TokenKind.RBrace))
        {
            return false;
        }

        if (reader.TryKeyword("diagram"))
        {
            diagram = DiagramParser.Parse(reader);
            return true;
        }

        if (!reader.TryPeekIdent(out var kind) || !DiagramKindRegistry.TryResolve(kind, out _))
        {
            return false;
        }

        diagram = DiagramParser.Parse(reader);
        return true;
    }

    private static bool TryParseSeriesTransform(TokenReader reader, out SeriesTransformBlock transform)
    {
        transform = null!;
        if (reader.TryKeyword("series"))
        {
            transform = ParseSeriesTransform(reader);
            return true;
        }

        if (!reader.TryKeyword("transform"))
        {
            return false;
        }

        if (!reader.TryKeyword("series"))
        {
            throw new DashSpecParseException("Expected 'series' after transform.");
        }

        transform = ParseSeriesTransform(reader);
        return true;
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
        var props = PropertyBlockParser.Parse(reader, PropertySchemas.SeriesTransform, "series");
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
