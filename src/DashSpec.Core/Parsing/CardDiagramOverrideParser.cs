using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class CardDiagramOverrideParser
{
    internal sealed record DiagramDelta(
        DiagramDefinition? Diagram,
        LegendDefinition? Legend,
        PresentationBlock? Presentation,
        SeriesTransformBlock? SeriesTransform);

    public static void ParseOverridesBlock(
        TokenReader reader,
        string cardId,
        bool plural,
        ref DiagramDefinition? diagram,
        ref LegendDefinition? legend,
        ref PresentationBlock? presentation,
        ref SeriesTransformBlock? seriesTransform)
    {
        if (plural)
        {
            BlockSyntax.BeginBlock(reader);
            reader.SkipNewlines();
            while (!BlockSyntax.IsBlockEnd(reader, "overrides") && !reader.IsEof)
            {
                reader.SkipNewlines();
                if (BlockSyntax.IsBlockEnd(reader, "overrides"))
                {
                    break;
                }

                if (!reader.TryKeyword("for"))
                {
                    throw reader.Unexpected("for <diagram_id>");
                }

                var targetId = reader.ReadIdent();
                var delta = ParseOverrideBody(reader, targetId, cardId, endKind: "for");
                ApplyDelta(ref diagram, ref legend, ref presentation, ref seriesTransform, delta, targetId, cardId);
            }

            BlockSyntax.ExpectBlockEnd(reader, "overrides");
            return;
        }

        if (!reader.TryKeyword("for"))
        {
            throw reader.Unexpected("for <diagram_id>");
        }

        var diagramId = reader.ReadIdent();
        var single = ParseOverrideBody(reader, diagramId, cardId, endKind: "override");
        ApplyDelta(ref diagram, ref legend, ref presentation, ref seriesTransform, single, diagramId, cardId);
    }

    public static DiagramDelta? TryParseDiagramInlineBody(
        TokenReader reader,
        string diagramId,
        string cardId,
        string parentEndKind)
    {
        reader.SkipNewlines();
        if (BlockSyntax.IsBlockEnd(reader, parentEndKind) || reader.IsEof)
        {
            return null;
        }

        if (reader.TryPeekIdent(out var next) &&
            (string.Equals(next, "diagram", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(next, "legend", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(next, "datasource", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(next, "bind", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(next, "chrome", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(next, "when", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(next, "layout", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(next, "override", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(next, "data", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(next, "view", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return ParseOverrideBody(reader, diagramId, cardId, endKind: "diagram", endId: diagramId);
    }

    private static DiagramDelta ParseOverrideBody(
        TokenReader reader,
        string diagramId,
        string cardId,
        string endKind,
        string? endId = null)
    {
        BlockSyntax.BeginBlock(reader);
        reader.SkipNewlines();

        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        LegendDefinition? legend = null;
        PresentationBlock? presentation = null;
        SeriesTransformBlock? seriesTransform = null;

        while (!BlockSyntax.IsBlockEnd(reader, endKind, endId) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, endKind, endId))
            {
                break;
            }

            if (reader.TryKeyword("series"))
            {
                seriesTransform = ParseSeriesOverride(reader, cardId, diagramId);
                continue;
            }

            if (reader.TryKeyword("legend"))
            {
                legend = ParseLegendOverride(reader);
                continue;
            }

            if (reader.TryKeyword("presentation"))
            {
                presentation = ParsePresentationOverride(reader);
                continue;
            }

            if (reader.TryPeekIdent(out var key) && reader.RawKind is TokenKind.Ident)
            {
                _ = reader.ReadIdent();
                reader.Expect(TokenKind.Eq);
                props[key] = reader.ReadScalarValue();
                reader.SkipNewlines();
                continue;
            }

            throw reader.Unexpected();
        }

        BlockSyntax.ExpectBlockEnd(reader, endKind, endId);

        var diagram = props.Count > 0
            ? new DiagramDefinition(string.Empty, props, diagramId)
            : null;

        return new DiagramDelta(diagram, legend, presentation, seriesTransform);
    }

    private static SeriesTransformBlock ParseSeriesOverride(TokenReader reader, string cardId, string diagramId)
    {
        if (reader.TryKeyword("max"))
        {
            reader.Expect(TokenKind.Eq);
            if (!int.TryParse(reader.ReadScalarValue(), out var max) || max <= 0)
            {
                throw new DashSpecParseException(
                    $"Card '{cardId}': override for '{diagramId}' series max must be a positive integer.");
            }

            return new SeriesTransformBlock(null, max, null);
        }

        if (reader.IsOnNewline())
        {
            reader.SkipNewlines();
            var props = PropertyBlockParser.Parse(reader, PropertySchemas.SeriesTransform, "series", endKind: "series");
            props.TryGetValue("use", out var usePreset);
            int? maxValue = null;
            if (props.TryGetValue("max", out var rawMax) &&
                int.TryParse(rawMax, out var parsedMax) &&
                parsedMax > 0)
            {
                maxValue = parsedMax;
            }

            props.TryGetValue("other", out var otherLabel);
            return new SeriesTransformBlock(usePreset, maxValue, otherLabel);
        }

        throw reader.Unexpected("max or multiline series block");
    }

    private static LegendDefinition ParseLegendOverride(TokenReader reader)
    {
        var props = PropertyBlockParser.Parse(reader, PropertySchemas.Legend, "legend", endKind: "legend");
        return new LegendDefinition(
            props.GetValueOrDefault("min"),
            props.GetValueOrDefault("max"),
            props.GetValueOrDefault("title"));
    }

    private static PresentationBlock ParsePresentationOverride(TokenReader reader)
    {
        var props = PropertyBlockParser.Parse(reader, PropertySchemas.Presentation, "presentation", endKind: "presentation");
        props.TryGetValue("use", out var usePreset);
        var inline = props
            .Where(x => !string.Equals(x.Key, "use", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        return new PresentationBlock(usePreset, inline);
    }

    private static void ApplyDelta(
        ref DiagramDefinition? diagram,
        ref LegendDefinition? legend,
        ref PresentationBlock? presentation,
        ref SeriesTransformBlock? seriesTransform,
        DiagramDelta delta,
        string targetId,
        string cardId)
    {
        if (diagram is not null &&
            !string.IsNullOrWhiteSpace(diagram.UsePreset) &&
            !string.Equals(diagram.UsePreset, targetId, StringComparison.OrdinalIgnoreCase))
        {
            throw new DashSpecParseException(
                $"Card '{cardId}': override for '{targetId}' does not match view diagram '{diagram.UsePreset}'.");
        }

        if (delta.Diagram is not null)
        {
            diagram = diagram is null
                ? delta.Diagram
                : SpecIncludeResolver.Merge(
                    new SpecIncludeFragment(delta.Diagram, null, null),
                    new SpecIncludeFragment(diagram, null, null)).Diagram;
        }

        if (delta.Legend is not null)
        {
            legend = delta.Legend;
        }

        if (delta.Presentation is not null)
        {
            presentation = presentation is null
                ? delta.Presentation
                : SpecIncludeResolver.Merge(
                    new SpecIncludeFragment(null, delta.Presentation, null),
                    new SpecIncludeFragment(null, presentation, null)).Presentation;
        }

        if (delta.SeriesTransform is not null)
        {
            seriesTransform = seriesTransform is null
                ? delta.SeriesTransform
                : SpecIncludeResolver.Merge(
                    new SpecIncludeFragment(null, null, delta.SeriesTransform),
                    new SpecIncludeFragment(null, null, seriesTransform)).SeriesTransform;
        }
    }
}
