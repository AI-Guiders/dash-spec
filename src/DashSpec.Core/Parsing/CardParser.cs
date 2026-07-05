using DashSpec.Core.Model;
using DashSpec.Core.Runtime;

namespace DashSpec.Core.Parsing;

internal static class CardParser
{
    public static CardDefinition Parse(
        TokenReader reader,
        IReadOnlyList<FilterDefinition> filters,
        string? specDirectory = null,
        ModuleIncludeState? includes = null,
        DashSpecParseOptions? parseOptions = null)
    {
        parseOptions ??= DashSpecParseOptions.Default;
        var id = reader.ReadIdent();
        if (!reader.TryKeyword("as"))
        {
            throw new DashSpecParseException($"Card '{id}' requires as \"Title\".");
        }

        var title = reader.ReadString();
        var layoutRef = ParserUtilities.TryReadLayoutRef(reader);

        reader.Expect(TokenKind.LBrace);
        reader.SkipNewlines();

        DiagramDefinition? diagram = null;
        DataSourceDefinition? dataSource = null;
        PlacementDefinition? placement = null;
        string? useCardPreset = null;
        var boundFilters = new List<string>();
        var localFilters = new List<string>();
        string? filterHostCardId = null;
        var hostedFilters = new List<string>();
        LegendDefinition? legend = null;
        PresentationBlock? presentation = null;
        SeriesTransformBlock? seriesTransform = null;
        LayoutBoardDefinition? interiorBoard = null;
        string? diagramSlotRef = null;
        CardClickBehaviour? clickBehaviour = null;
        var extensionBlocks = new List<ExtensionBlockNode>();
        var localFiltersManualApply = false;
        var includeFragment = new SpecIncludeFragment(null, null, null);

        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            if (reader.TryKeyword("on"))
            {
                if (!reader.TryKeyword("click"))
                {
                    throw new DashSpecParseException(
                        $"Card '{id}': only 'on click' is supported in v1.");
                }

                if (clickBehaviour is not null)
                {
                    throw new DashSpecParseException($"Card '{id}': duplicate on click block.");
                }

                clickBehaviour = CardClickParser.ParseClickBlock(reader, id, parseOptions);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("include"))
            {
                if (string.IsNullOrWhiteSpace(specDirectory))
                {
                    throw new DashSpecParseException(
                        $"Card '{id}': include requires spec directory when parsing (path to the .dashspec folder).");
                }

                var (kind, reference) = DiagramModuleParser.ReadIncludeReference(reader);
                includeFragment = SpecIncludeResolver.Merge(
                    includeFragment,
                    SpecIncludeResolver.Load(kind, reference, specDirectory));
                reader.SkipNewlines();
                continue;
            }

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
                if (reader.TryKeyword("host"))
                {
                    var hostCardId = reader.ReadIdent();
                    if (filterHostCardId is not null)
                    {
                        throw new DashSpecParseException(
                            $"Card '{id}' declares more than one filters host block.");
                    }

                    filterHostCardId = hostCardId;
                    hostedFilters.AddRange(ParseFilterPlacementList(reader, "filters host"));
                }
                else if (reader.IsAt(TokenKind.LBrace))
                {
                    var parsed = ParseLocalFiltersBlock(reader, id);
                    localFilters.AddRange(parsed.FilterNames);
                    localFiltersManualApply = parsed.ManualApply;
                }
                else
                {
                    localFilters.AddRange(reader.ReadCommaListInline());
                }

                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("diagram"))
            {
                diagramSlotRef ??= ParserUtilities.TryReadLayoutRef(reader);
                if (!reader.TryPeekIdent(out var diagramName))
                {
                    throw reader.Unexpected("diagram kind, preset id, or registry id");
                }

                _ = reader.ReadIdent();
                reader.SkipNewlines();
                if (reader.IsAt(TokenKind.LBrace))
                {
                    diagram = DiagramParser.ParseAfterKindIdent(reader, diagramName);
                }
                else if (includes is not null && includes.TryGetDiagram(diagramName, out var registered))
                {
                    includeFragment = SpecIncludeResolver.Merge(includeFragment, registered);
                }
                else
                {
                    diagram = new DiagramDefinition(
                        string.Empty,
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                        diagramName);
                }

                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("place"))
            {
                placement = LayoutParser.ParsePlacement(reader);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("layout"))
            {
                if (reader.TryPeekIdent(out var layoutNext) &&
                    string.Equals(layoutNext, "grid", StringComparison.OrdinalIgnoreCase))
                {
                    throw new DashSpecParseException(
                        $"Card '{id}': use dashboard wiring for layout grid; card layout is a bracket board only.");
                }

                interiorBoard = LayoutParser.ParseBoard(reader);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("datasource"))
            {
                dataSource = DataSourceParser.Parse(reader, specDirectory);
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

            if (reader.TryPeekIdent(out var extensionKeyword) &&
                parseOptions.ExtensionBlockKeywords.Contains(extensionKeyword))
            {
                extensionBlocks.Add(
                    ExtensionBlockParser.Parse(reader, extensionKeyword, parseOptions.ExtensionBlockKeywords));
                ValidateExtensionBlock(extensionBlocks[^1], id, parseOptions);
                reader.SkipNewlines();
                continue;
            }

            throw reader.Unexpected();
        }

        reader.Expect(TokenKind.RBrace);

        if (includeFragment.Diagram is not null)
        {
            diagram = diagram is null
                ? includeFragment.Diagram
                : SpecIncludeResolver.Merge(
                    new SpecIncludeFragment(diagram, null, null),
                    new SpecIncludeFragment(includeFragment.Diagram, null, null)).Diagram;
        }

        if (includeFragment.Presentation is not null)
        {
            presentation = presentation is null
                ? includeFragment.Presentation
                : SpecIncludeResolver.Merge(
                    new SpecIncludeFragment(null, presentation, null),
                    new SpecIncludeFragment(null, includeFragment.Presentation, null)).Presentation;
        }

        if (includeFragment.SeriesTransform is not null)
        {
            seriesTransform = seriesTransform is null
                ? includeFragment.SeriesTransform
                : SpecIncludeResolver.Merge(
                    new SpecIncludeFragment(null, null, seriesTransform),
                    new SpecIncludeFragment(null, null, includeFragment.SeriesTransform)).SeriesTransform;
        }

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
            LayoutRef: layoutRef,
            UseCardPreset: useCardPreset,
            Legend: legend,
            Presentation: presentation,
            SeriesTransform: seriesTransform,
            FilterHostCardId: filterHostCardId,
            HostedFilters: hostedFilters,
            InteriorBoard: interiorBoard,
            DiagramSlotRef: diagramSlotRef,
            ClickBehaviour: clickBehaviour,
            ExtensionBlocks: extensionBlocks,
            LocalFiltersManualApply: localFiltersManualApply);
    }

    private static (IReadOnlyList<string> FilterNames, bool ManualApply) ParseLocalFiltersBlock(
        TokenReader reader,
        string cardId)
    {
        reader.Expect(TokenKind.LBrace);
        reader.SkipNewlines();

        var names = new List<string>();
        var manualApply = false;
        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.IsAt(TokenKind.RBrace))
            {
                break;
            }

            if (reader.TryKeyword("apply"))
            {
                reader.Expect(TokenKind.Eq);
                var mode = reader.ReadIdent();
                if (!string.Equals(mode, "manual", StringComparison.OrdinalIgnoreCase))
                {
                    throw new DashSpecParseException(
                        $"Card '{cardId}': filters apply must be 'manual', got '{mode}'.");
                }

                manualApply = true;
                reader.SkipNewlines();
                continue;
            }

            names.Add(reader.ReadIdent());
            reader.SkipNewlines();
            if (reader.CurrentKind is TokenKind.Comma)
            {
                reader.Advance();
            }
        }

        reader.SkipNewlines();
        reader.Expect(TokenKind.RBrace);
        if (names.Count == 0)
        {
            throw new DashSpecParseException($"Card '{cardId}': filters block requires at least one filter name.");
        }

        return (names, manualApply);
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

    private static void ValidateExtensionBlock(
        ExtensionBlockNode block,
        string cardId,
        DashSpecParseOptions parseOptions)
    {
        if (parseOptions.KnownActionHandlers.Count > 0 &&
            block.Properties.TryGetValue("action", out var action) &&
            !parseOptions.KnownActionHandlers.Contains(action))
        {
            throw new DashSpecParseException(
                $"Card '{cardId}': unknown action handler '{action}'.");
        }

        foreach (var nested in block.Nested)
        {
            ValidateExtensionBlock(nested, cardId, parseOptions);
        }
    }
}
