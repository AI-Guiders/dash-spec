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
        DashSpecParseOptions? parseOptions = null,
        string? phaseId = null,
        string? pageId = null)
    {
        parseOptions ??= DashSpecParseOptions.Default;
        var id = reader.ReadIdent();
        string? title = null;

        if (reader.TryKeywordSameLine("as"))
        {
            title = reader.ReadString();
        }

        var layoutRef = ParserUtilities.TryReadLayoutRef(reader);

        BlockSyntax.BeginBlock(reader);
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
        CardVisibilityRule? visibility = null;
        MatrixRenderLimitsDefinition? matrixLimits = null;
        string? oversizeMessage = null;
        CardChromeDefinition? chrome = null;
        var extensionBlocks = new List<ExtensionBlockNode>();
        var localFiltersManualApply = false;
        var includeFragment = new SpecIncludeFragment(null, null, null);
        InspectPresentation? inspect = null;
        TooltipDefinition? tooltip = null;
        var inlineTooltips = new Dictionary<string, TooltipDefinition>(StringComparer.OrdinalIgnoreCase);

        while (!BlockSyntax.IsBlockEnd(reader, "card", id) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, "card", id))
            {
                break;
            }

            if (reader.TryKeyword("title"))
            {
                reader.Expect(TokenKind.Eq);
                title = reader.ReadString();
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("chrome"))
            {
                if (chrome is not null)
                {
                    throw new DashSpecParseException($"Card '{id}': duplicate chrome block.");
                }

                chrome = CardChromeParser.Parse(reader, id);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("when"))
            {
                var whenTarget = reader.ReadIdent();
                if (string.Equals(whenTarget, "oversize", StringComparison.OrdinalIgnoreCase))
                {
                    if (oversizeMessage is not null)
                    {
                        throw new DashSpecParseException($"Card '{id}': duplicate when oversize block.");
                    }

                    oversizeMessage = CardVisibilityParser.ParseOversizeWhen(reader, id);
                }
                else
                {
                    if (visibility is not null)
                    {
                        throw new DashSpecParseException($"Card '{id}': duplicate when block.");
                    }

                    visibility = CardVisibilityParser.ParseFilterWhen(reader, id, whenTarget);
                }

                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("limits"))
            {
                if (matrixLimits is not null)
                {
                    throw new DashSpecParseException($"Card '{id}': duplicate limits block.");
                }

                matrixLimits = CardLimitsParser.Parse(reader, id);
                reader.SkipNewlines();
                continue;
            }

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

            if (reader.TryKeyword("data"))
            {
                ParseDataBlock(reader, id, specDirectory, ref dataSource, ref boundFilters);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("view"))
            {
                ParseViewBlock(
                    reader,
                    id,
                    specDirectory,
                    includes,
                    parseOptions,
                    ref diagram,
                    ref diagramSlotRef,
                    ref legend,
                    ref presentation,
                    ref seriesTransform,
                    ref includeFragment);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("override"))
            {
                CardDiagramOverrideParser.ParseOverridesBlock(
                    reader,
                    id,
                    plural: false,
                    ref diagram,
                    ref legend,
                    ref presentation,
                    ref seriesTransform);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("overrides"))
            {
                CardDiagramOverrideParser.ParseOverridesBlock(
                    reader,
                    id,
                    plural: true,
                    ref diagram,
                    ref legend,
                    ref presentation,
                    ref seriesTransform);
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
                if (reader.TryKeywordSameLine("host"))
                {
                    var hostCardId = reader.ReadIdent();
                    if (filterHostCardId is not null)
                    {
                        throw new DashSpecParseException(
                            $"Card '{id}' declares more than one filters host block.");
                    }

                    filterHostCardId = hostCardId;
                    hostedFilters.AddRange(ParseFilterPlacementList(reader, "filters", "filters host"));
                }
                else if (reader.IsOnNewline() ||
                         (reader.TryPeekIdent(out var afterFilters) &&
                          string.Equals(afterFilters, "apply", StringComparison.OrdinalIgnoreCase)))
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
                ParseDiagramStatement(
                    reader,
                    id,
                    includes,
                    parentEndKind: "card",
                    parentEndId: id,
                    ref diagramSlotRef,
                    ref diagram,
                    ref legend,
                    ref presentation,
                    ref seriesTransform,
                    ref includeFragment);
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

                var (parsedPlacement, parsedBoard) = ParseCardLayoutContainer(reader, id);
                placement ??= parsedPlacement;
                interiorBoard = parsedBoard ?? interiorBoard;
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

            if (reader.TryKeyword("inspect"))
            {
                inspect = InspectPresentationParser.Merge(
                    inspect,
                    InspectPresentationParser.Parse(reader, $"Card '{id}'"));
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("tooltip"))
            {
                var tooltipId = reader.ReadIdent();
                if (string.IsNullOrWhiteSpace(tooltipId))
                {
                    throw new DashSpecParseException($"Card '{id}': inline tooltip requires an id.");
                }

                inlineTooltips[tooltipId] = TooltipModuleParser.ParseInline(reader, tooltipId);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("transform"))
            {
                if (!reader.TryKeyword("series"))
                {
                    throw new DashSpecParseException("Expected 'series' after transform.");
                }

                if (reader.TryKeyword("max"))
                {
                    reader.Expect(TokenKind.Eq);
                    if (!int.TryParse(reader.ReadScalarValue(), out var max) || max <= 0)
                    {
                        throw new DashSpecParseException($"Card '{id}': transform series max must be a positive integer.");
                    }

                    seriesTransform = new SeriesTransformBlock(null, max, null);
                }
                else
                {
                    reader.SkipNewlines();
                    seriesTransform = ParseSeriesTransform(reader);
                }

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

        BlockSyntax.ExpectBlockEnd(reader, "card", id);

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DashSpecParseException($"Card '{id}' requires title (as \"…\" or title = \"…\").");
        }

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
                    new SpecIncludeFragment(null, null, includeFragment.SeriesTransform),
                    new SpecIncludeFragment(null, null, seriesTransform)).SeriesTransform;
        }

        inspect = InspectPresentationParser.Merge(includeFragment.Inspect, inspect);
        tooltip = ResolveCardTooltip(inspect, inlineTooltips, includeFragment.Tooltips, tooltip);

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
            LocalFiltersManualApply: localFiltersManualApply,
            Visibility: visibility,
            PhaseId: phaseId,
            PageId: pageId,
            MatrixLimits: matrixLimits,
            OversizeMessage: oversizeMessage,
            Chrome: chrome,
            Inspect: inspect,
            Tooltip: tooltip);
    }

    private static TooltipDefinition? ResolveCardTooltip(
        InspectPresentation? inspect,
        IReadOnlyDictionary<string, TooltipDefinition> inlineTooltips,
        IReadOnlyDictionary<string, TooltipDefinition>? includeTooltips,
        TooltipDefinition? explicitTooltip)
    {
        if (explicitTooltip is not null)
        {
            return explicitTooltip;
        }

        var merged = new Dictionary<string, TooltipDefinition>(StringComparer.OrdinalIgnoreCase);
        if (includeTooltips is not null)
        {
            foreach (var (key, value) in includeTooltips)
            {
                merged[key] = value;
            }
        }

        foreach (var (key, value) in inlineTooltips)
        {
            merged[key] = value;
        }

        if (inspect?.TooltipId is not { } tooltipId)
        {
            return null;
        }

        return merged.TryGetValue(tooltipId, out var resolved) ? resolved : null;
    }

    private static void ParseDataBlock(
        TokenReader reader,
        string cardId,
        string? specDirectory,
        ref DataSourceDefinition? dataSource,
        ref List<string> boundFilters)
    {
        BlockSyntax.BeginBlock(reader);
        reader.SkipNewlines();

        while (!BlockSyntax.IsBlockEnd(reader, "data") && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, "data"))
            {
                break;
            }

            if (reader.TryKeyword("datasource"))
            {
                dataSource = DataSourceParser.Parse(reader, specDirectory);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("bind"))
            {
                boundFilters.AddRange(ParseBind(reader));
                reader.SkipNewlines();
                continue;
            }

            throw reader.Unexpected("datasource or bind");
        }

        BlockSyntax.ExpectBlockEnd(reader, "data");
    }

    private static void ParseViewBlock(
        TokenReader reader,
        string cardId,
        string? specDirectory,
        ModuleIncludeState? includes,
        DashSpecParseOptions parseOptions,
        ref DiagramDefinition? diagram,
        ref string? diagramSlotRef,
        ref LegendDefinition? legend,
        ref PresentationBlock? presentation,
        ref SeriesTransformBlock? seriesTransform,
        ref SpecIncludeFragment includeFragment)
    {
        BlockSyntax.BeginBlock(reader);
        reader.SkipNewlines();

        while (!BlockSyntax.IsBlockEnd(reader, "view") && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, "view"))
            {
                break;
            }

            if (reader.TryKeyword("diagram"))
            {
                ParseDiagramStatement(
                    reader,
                    cardId,
                    includes,
                    parentEndKind: "view",
                    parentEndId: null,
                    ref diagramSlotRef,
                    ref diagram,
                    ref legend,
                    ref presentation,
                    ref seriesTransform,
                    ref includeFragment);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("legend"))
            {
                legend = ParseLegend(reader);
                reader.SkipNewlines();
                continue;
            }

            throw reader.Unexpected("diagram or legend");
        }

        BlockSyntax.ExpectBlockEnd(reader, "view");
    }

    private static (PlacementDefinition? Placement, LayoutBoardDefinition? Board) ParseCardLayoutContainer(
        TokenReader reader,
        string cardId)
    {
        BlockSyntax.BeginBlock(reader);
        reader.SkipNewlines();

        PlacementDefinition? placement = null;
        LayoutBoardDefinition? board = null;

        while (!BlockSyntax.IsBlockEnd(reader, "layout") && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, "layout"))
            {
                break;
            }

            if (reader.TryKeyword("place"))
            {
                placement = LayoutParser.ParsePlacement(reader);
                reader.SkipNewlines();
                continue;
            }

            if (reader.IsAt(TokenKind.LBracket))
            {
                board = LayoutParser.ParseBoardRows(reader, "layout");
                break;
            }

            throw reader.Unexpected("place or [");
        }

        BlockSyntax.ExpectBlockEnd(reader, "layout");
        return (placement, board);
    }

    private static void ParseDiagramStatement(
        TokenReader reader,
        string cardId,
        ModuleIncludeState? includes,
        string parentEndKind,
        string? parentEndId,
        ref string? diagramSlotRef,
        ref DiagramDefinition? diagram,
        ref LegendDefinition? legend,
        ref PresentationBlock? presentation,
        ref SeriesTransformBlock? seriesTransform,
        ref SpecIncludeFragment includeFragment)
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
            throw new DashSpecParseException(
                $"Card '{cardId}': brace diagram blocks removed; use diagram {diagramName} … end diagram.");
        }

        if (includes is not null && includes.TryGetDiagram(diagramName, out var registered))
        {
            includeFragment = SpecIncludeResolver.Merge(includeFragment, registered);
        }
        else if (DiagramKindRegistry.TryResolve(diagramName, out _))
        {
            diagram = DiagramParser.ParseAfterKindIdent(reader, diagramName);
            return;
        }
        else
        {
            diagram = new DiagramDefinition(
                string.Empty,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                diagramName);
        }

        var inlineDelta = CardDiagramOverrideParser.TryParseDiagramInlineBody(
            reader,
            diagramName,
            cardId,
            parentEndKind);

        if (inlineDelta is null)
        {
            return;
        }

        if (inlineDelta.Diagram is not null)
        {
            diagram = SpecIncludeResolver.Merge(
                new SpecIncludeFragment(diagram, null, null),
                new SpecIncludeFragment(inlineDelta.Diagram, null, null)).Diagram;
        }

        if (inlineDelta.Legend is not null)
        {
            legend = inlineDelta.Legend;
        }

        if (inlineDelta.Presentation is not null)
        {
            presentation = presentation is null
                ? inlineDelta.Presentation
                : SpecIncludeResolver.Merge(
                    new SpecIncludeFragment(null, inlineDelta.Presentation, null),
                    new SpecIncludeFragment(null, presentation, null)).Presentation;
        }

        if (inlineDelta.SeriesTransform is not null)
        {
            seriesTransform = seriesTransform is null
                ? inlineDelta.SeriesTransform
                : SpecIncludeResolver.Merge(
                    new SpecIncludeFragment(null, null, inlineDelta.SeriesTransform),
                    new SpecIncludeFragment(null, null, seriesTransform)).SeriesTransform;
        }
    }

    private static SeriesTransformBlock ParseSeriesTransformInline(TokenReader reader, string cardId)
    {
        reader.ExpectKeyword("max");
        reader.Expect(TokenKind.Eq);
        if (!int.TryParse(reader.ReadScalarValue(), out var max) || max <= 0)
        {
            throw new DashSpecParseException($"Card '{cardId}': transform series max must be a positive integer.");
        }

        return new SeriesTransformBlock(null, max, null);
    }

    private static (IReadOnlyList<string> FilterNames, bool ManualApply) ParseLocalFiltersBlock(
        TokenReader reader,
        string cardId)
    {
        BlockSyntax.BeginBlock(reader);
        reader.SkipNewlines();

        var names = new List<string>();
        var manualApply = false;
        while (!BlockSyntax.IsBlockEnd(reader, "filters") && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, "filters"))
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
        BlockSyntax.ExpectBlockEnd(reader, "filters");
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

    private static IReadOnlyList<string> ParseFilterPlacementList(TokenReader reader, string endKind, string blockName)
    {
        if (reader.IsOnNewline())
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, endKind))
            {
                BlockSyntax.ExpectBlockEnd(reader, endKind);
                return [];
            }

            return PropertyBlockParser.ParseCommaListBlock(reader, endKind, blockName);
        }

        return reader.ReadCommaListInline();
    }

    private static IReadOnlyList<string> ParseBind(TokenReader reader)
    {
        if (reader.IsOnNewline())
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, "bind"))
            {
                BlockSyntax.ExpectBlockEnd(reader, "bind");
                return [];
            }

            return PropertyBlockParser.ParseCommaListBlock(reader, "bind", "bind");
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
