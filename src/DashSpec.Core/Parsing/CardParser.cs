using DashSpec.Core.Model;
using DashSpec.Core.Runtime;

namespace DashSpec.Core.Parsing;

internal static class CardParser
{
    public static CardDefinition Parse(TokenReader reader, IReadOnlyList<FilterDefinition> filters)
    {
        var id = reader.ReadIdent();
        if (!reader.TryKeyword("as"))
        {
            throw new DashSpecParseException($"Card '{id}' requires as \"Title\".");
        }

        var title = reader.ReadString();
        reader.Expect(TokenKind.LBrace);
        reader.SkipNewlines();

        DiagramDefinition? diagram = null;
        DataSourceDefinition? dataSource = null;
        PlacementDefinition? placement = null;
        string? useCardPreset = null;
        var boundFilters = new List<string>();
        var localFilters = new List<string>();
        LegendDefinition? legend = null;
        PresentationBlock? presentation = null;
        SeriesTransformBlock? seriesTransform = null;

        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            if (reader.TryKeyword("use"))
            {
                useCardPreset = reader.ReadIdent();
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("bind"))
            {
                boundFilters.AddRange(ParseBind(reader));
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("filters"))
            {
                localFilters.AddRange(ParseFilterPlacementList(reader, "filters"));
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("diagram"))
            {
                diagram = DiagramParser.Parse(reader);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("place"))
            {
                placement = LayoutParser.ParsePlacement(reader);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("datasource"))
            {
                dataSource = DataSourceParser.Parse(reader);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("legend"))
            {
                legend = ParseLegend(reader);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("presentation"))
            {
                presentation = ParsePresentation(reader);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("transform"))
            {
                if (!reader.TryKeyword("series"))
                {
                    throw new DashSpecParseException("Expected 'series' after transform.");
                }

                seriesTransform = ParseSeriesTransform(reader);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("where"))
            {
                throw new DashSpecParseException(
                    $"Card '{id}': 'where' is no longer used — list filters in bind …; " +
                    "Core compiles WHERE from bind (date/field → AND …, top → TOP/LIMIT).");
            }

            throw reader.Unexpected();
        }

        reader.Expect(TokenKind.RBrace);

        if (diagram is null && useCardPreset is null)
        {
            throw new DashSpecParseException("Card requires a diagram block or use <card-preset>.");
        }

        if (dataSource is null && useCardPreset is null)
        {
            throw new DashSpecParseException("Card requires a datasource block or use <card-preset>.");
        }

        ValidateCardFilterBinding(id, boundFilters, filters, useCardPreset);

        return new CardDefinition(
            id,
            title,
            diagram ?? new DiagramDefinition(string.Empty, new Dictionary<string, string>()),
            dataSource ?? new DataSourceDefinition(DataSourceKind.View, string.Empty),
            boundFilters,
            localFilters,
            placement,
            UseCardPreset: useCardPreset,
            Legend: legend,
            Presentation: presentation,
            SeriesTransform: seriesTransform);
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

    private static LegendDefinition ParseLegend(TokenReader reader)
    {
        var props = PropertyBlockParser.Parse(reader, PropertySchemas.Legend, "legend");
        return new LegendDefinition(
            props.GetValueOrDefault("min"),
            props.GetValueOrDefault("max"),
            props.GetValueOrDefault("title"));
    }

    private static IReadOnlyList<string> ParseFilterPlacementList(TokenReader reader, string blockName)
    {
        if (reader.IsAt(TokenKind.LBrace))
        {
            return PropertyBlockParser.ParseCommaListBlock(reader, blockName);
        }

        return reader.ReadCommaListInline();
    }

    private static IReadOnlyList<string> ParseBind(TokenReader reader)
    {
        if (reader.IsAt(TokenKind.LBrace))
        {
            return PropertyBlockParser.ParseCommaListBlock(reader, "bind");
        }

        return reader.ReadCommaListInline();
    }

    private static void ValidateCardFilterBinding(
        string cardId,
        IReadOnlyList<string> boundFilters,
        IReadOnlyList<FilterDefinition> filters,
        string? useCardPreset)
    {
        if (boundFilters.Count == 0)
        {
            return;
        }

        var registry = filters.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var name in boundFilters)
        {
            if (string.Equals(name, CardBindResolver.DashboardToken, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!registry.ContainsKey(name))
            {
                throw new DashSpecParseException(
                    $"Card '{cardId}': bind references unknown filter '{name}'.");
            }
        }
    }
}
