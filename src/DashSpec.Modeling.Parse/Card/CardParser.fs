namespace DashSpec.Modeling.Parse.Card

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.DataSource
open DashSpec.Modeling.Parse.Diagram
open DashSpec.Modeling.Parse.Filter
open DashSpec.Modeling.Parse.Include
open DashSpec.Modeling.Parse.Layout
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Presentation
open DashSpec.Modeling.Parse.Tooltip
open DashSpec.Modeling.Parse.Filter
open DashSpec.Modeling.Parse.Transform

module CardParser =

    let private propValue (props: IDictionary<string, string>) key =
        match props.TryGetValue key with
        | true, value -> Some value
        | false, _ -> None

    let private propBool (props: IDictionary<string, string>) key =
        match props.TryGetValue key with
        | true, value when value.Equals("false", StringComparison.OrdinalIgnoreCase) -> Some false
        | true, value when value.Equals("true", StringComparison.OrdinalIgnoreCase) -> Some true
        | true, _ -> Some true
        | false, _ -> None

    let private parsePresentation (reader: TokenReader) =
        let props = PropertyBlockParser.parse reader PropertySchemas.presentation "presentation" false false
        let usePreset = propValue props "use"
        let inlineProps =
            let map = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            for kv in props do
                if not (kv.Key.Equals("use", StringComparison.OrdinalIgnoreCase)) then
                    map.[kv.Key] <- kv.Value
            map :> IReadOnlyDictionary<_, _>
        { UsePreset = usePreset; Properties = inlineProps }

    let private parseSeriesTransform (reader: TokenReader) =
        let props = PropertyBlockParser.parse reader PropertySchemas.seriesTransform "transform series" false false
        let usePreset = propValue props "use"
        let max =
            match propValue props "max" with
            | Some raw ->
                let mutable parsed = 0
                if Int32.TryParse(raw, &parsed) && parsed > 0 then Some parsed else None
            | None -> None
        let other = propValue props "other"
        { UsePreset = usePreset; Max = max; OtherLabel = other }

    let private parseLegend (reader: TokenReader) =
        let props = PropertyBlockParser.parse reader PropertySchemas.legend "legend" false false
        { MinLabel = propValue props "min"
          MaxLabel = propValue props "max"
          Title = propValue props "title"
          Show = propBool props "show" }

    let private parseBind (reader: TokenReader) =
        if reader.IsOnNewline() then
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "bind" None then
                BlockSyntax.expectBlockEnd reader "bind" None
                Array.empty :> IReadOnlyList<_>
            else
                PropertyBlockParser.parseCommaListBlock reader "bind" "bind"
        else
            reader.ReadCommaListInline()

    let private parseFilterPlacementList (reader: TokenReader) (endKind: string) (blockName: string) =
        if reader.IsOnNewline() then
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader endKind None then
                BlockSyntax.expectBlockEnd reader endKind None
                Array.empty :> IReadOnlyList<_>
            else
                PropertyBlockParser.parseCommaListBlock reader endKind blockName
        else
            reader.ReadCommaListInline()

    let private parseLocalFiltersBlock (reader: TokenReader) (cardId: string) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let names = ResizeArray<string>()
        let mutable manualApply = false
        let mutable applySplitIndex = None
        let mutable namesBeforeApply = 0
        let chromeBoard = ref None

        while not (BlockSyntax.isBlockEnd reader "filters" None) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "filters" None then ()
            elif reader.TryKeyword "layout" then
                if chromeBoard.Value.IsSome then
                    raise (DashSpecParseException($"Card '{cardId}': duplicate filters layout block."))
                chromeBoard.Value <- Some(LayoutParser.parseBoard reader)
            elif reader.TryKeyword "apply" then
                reader.Expect TokenKind.Eq
                let mode = reader.ReadIdent()
                if not (String.Equals(mode, "manual", StringComparison.OrdinalIgnoreCase)) then
                    raise (DashSpecParseException($"Card '{cardId}': filters apply must be 'manual', got '{mode}'."))
                manualApply <- true
                if applySplitIndex.IsNone then
                    applySplitIndex <- Some namesBeforeApply
                reader.SkipNewlines()
            else
                names.Add(reader.ReadIdent())
                if applySplitIndex.IsNone then
                    namesBeforeApply <- namesBeforeApply + 1
                reader.SkipNewlines()
                if reader.CurrentKind = TokenKind.Comma then reader.Advance()

        reader.SkipNewlines()
        BlockSyntax.expectBlockEnd reader "filters" None
        if names.Count = 0 then
            raise (DashSpecParseException($"Card '{cardId}': filters block requires at least one filter name."))
        names :> IReadOnlyList<_>, manualApply, applySplitIndex, chromeBoard.Value

    let private parseDataBlock
        (reader: TokenReader)
        (cardId: string)
        (specDirectory: string option)
        (dataSource: DataSourceDefinition option ref)
        (boundFilters: ResizeArray<string>)
        =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        while not (BlockSyntax.isBlockEnd reader "data" None) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "data" None then ()
            elif reader.TryKeyword "datasource" then
                dataSource.Value <- Some(DataSourceParser.parse reader specDirectory)
                reader.SkipNewlines()
            elif reader.TryKeyword "bind" then
                boundFilters.AddRange(parseBind reader)
                reader.SkipNewlines()
            else
                raise (reader.Unexpected "datasource or bind")
        BlockSyntax.expectBlockEnd reader "data" None

    let private parseDataBlockForSlot
        (reader: TokenReader)
        (cardId: string)
        (specDirectory: string option)
        =
        let dataSource = ref None
        let boundFilters = ResizeArray<string>()
        parseDataBlock reader cardId specDirectory dataSource boundFilters
        match dataSource.Value with
        | None -> raise (DashSpecParseException($"Card '{cardId}': data block requires datasource."))
        | Some ds -> ds, boundFilters :> IReadOnlyList<_>

    let private parseDiagramStatement
        (reader: TokenReader)
        (cardId: string)
        (includes: ModuleIncludeState option)
        (parentEndKind: string)
        (diagramSlotRef: string option ref)
        (diagram: DiagramDefinition option ref)
        (legend: LegendDefinition option ref)
        (presentation: PresentationBlock option ref)
        (seriesTransform: SeriesTransformBlock option ref)
        (includeFragment: SpecIncludeFragment ref)
        =
        if diagramSlotRef.Value.IsNone then
            diagramSlotRef.Value <- ParserUtilities.tryReadLayoutRef reader

        match reader.TryPeekIdent() with
        | None -> raise (reader.Unexpected "diagram kind, preset id, or registry id")
        | Some diagramName ->
            reader.ReadIdent() |> ignore
            reader.SkipNewlines()
            if reader.IsAt TokenKind.LBrace then
                raise (DashSpecParseException($"Card '{cardId}': brace diagram blocks removed; use diagram {diagramName} … end diagram."))

            let mutable registered = SpecIncludeFragmentResolver.emptyFragment
            let mutable parsedKindRegistry = false

            match includes with
            | Some state when state.TryGetDiagram(diagramName, &registered) ->
                includeFragment.Value <- SpecIncludeFragmentResolver.merge includeFragment.Value registered
                if registered.Diagram.IsSome then
                    diagram.Value <-
                        match diagram.Value with
                        | None -> registered.Diagram
                        | Some current ->
                            (SpecIncludeFragmentResolver.merge
                                { SpecIncludeFragmentResolver.emptyFragment with Diagram = Some current }
                                { SpecIncludeFragmentResolver.emptyFragment with Diagram = registered.Diagram }).Diagram
            | _ ->
                if DiagramKindRegistry.tryResolve diagramName |> fst then
                    diagram.Value <- Some(DiagramParser.parseAfterKindIdent reader diagramName)
                    parsedKindRegistry <- true
                else
                    diagram.Value <-
                        Some
                            { Kind = ""
                              Properties = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) :> IReadOnlyDictionary<_, _>
                              UsePreset = Some diagramName }

            if not parsedKindRegistry then
                match CardDiagramOverrideParser.tryParseDiagramInlineBody reader diagramName cardId parentEndKind with
                | None -> ()
                | Some inlineDelta ->
                    if inlineDelta.Diagram.IsSome then
                        diagram.Value <-
                            match diagram.Value with
                            | None -> inlineDelta.Diagram
                            | Some current ->
                                (SpecIncludeFragmentResolver.merge
                                    { SpecIncludeFragmentResolver.emptyFragment with Diagram = Some current }
                                    { SpecIncludeFragmentResolver.emptyFragment with Diagram = inlineDelta.Diagram }).Diagram
                    if inlineDelta.Legend.IsSome then legend.Value <- inlineDelta.Legend
                    if inlineDelta.Presentation.IsSome then
                        presentation.Value <-
                            match presentation.Value with
                            | None -> inlineDelta.Presentation
                            | Some current ->
                                (SpecIncludeFragmentResolver.merge
                                    { SpecIncludeFragmentResolver.emptyFragment with Presentation = Some current }
                                    { SpecIncludeFragmentResolver.emptyFragment with Presentation = inlineDelta.Presentation }).Presentation
                    if inlineDelta.SeriesTransform.IsSome then
                        seriesTransform.Value <-
                            match seriesTransform.Value with
                            | None -> inlineDelta.SeriesTransform
                            | Some current ->
                                (SpecIncludeFragmentResolver.merge
                                    { SpecIncludeFragmentResolver.emptyFragment with SeriesTransform = Some current }
                                    { SpecIncludeFragmentResolver.emptyFragment with SeriesTransform = inlineDelta.SeriesTransform }).SeriesTransform

    let private parseViewBlock
        (reader: TokenReader)
        (cardId: string)
        (specDirectory: string option)
        (includes: ModuleIncludeState option)
        (parseOptions: DashSpecParseOptions)
        (slotBuilder: CardDiagramSlotBuilder.Builder)
        (diagram: DiagramDefinition option ref)
        (diagramSlotRef: string option ref)
        (legend: LegendDefinition option ref)
        (presentation: PresentationBlock option ref)
        (seriesTransform: SeriesTransformBlock option ref)
        (includeFragment: SpecIncludeFragment ref)
        =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        while not (BlockSyntax.isBlockEnd reader "view" None) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "view" None then ()
            elif reader.TryKeyword "diagram" then
                let slotRef =
                    match ParserUtilities.tryReadLayoutRef reader with
                    | Some r ->
                        if diagramSlotRef.Value.IsNone then diagramSlotRef.Value <- Some r
                        r
                    | None -> CardDiagramSlotBuilder.resolveSlotRef slotBuilder diagramSlotRef.Value
                parseDiagramStatement reader cardId includes "view" diagramSlotRef diagram legend presentation seriesTransform includeFragment
                match diagram.Value with
                | Some d ->
                    CardDiagramSlotBuilder.touchDiagram slotBuilder slotRef d legend.Value presentation.Value seriesTransform.Value
                | None -> ()
                reader.SkipNewlines()
            elif reader.TryKeyword "legend" then
                legend.Value <- Some(parseLegend reader)
                reader.SkipNewlines()
            else
                raise (reader.Unexpected "diagram or legend")
        BlockSyntax.expectBlockEnd reader "view" None

    let private parseCardLayoutContainer (reader: TokenReader) (cardId: string) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let mutable placement: PlacementDefinition option = None
        let mutable board: LayoutBoardDefinition option = None
        while not (BlockSyntax.isBlockEnd reader "layout" None) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "layout" None then ()
            elif reader.TryKeyword "place" then
                placement <- Some(LayoutParser.parsePlacement reader)
                reader.SkipNewlines()
            elif reader.IsAt TokenKind.LBracket then
                board <- Some(LayoutParser.parseBoardRows reader (Some "layout") None)
            else
                raise (reader.Unexpected "place or [")
        BlockSyntax.expectBlockEnd reader "layout" None
        placement, board

    let private resolveCardTooltip
        (inspect: InspectPresentation option)
        (inlineTooltips: IReadOnlyDictionary<string, TooltipDefinition>)
        (includeTooltips: IReadOnlyDictionary<string, TooltipDefinition> option)
        (explicitTooltip: TooltipDefinition option)
        =
        match explicitTooltip with
        | Some tooltip -> Some tooltip
        | None ->
            let merged = Dictionary<string, TooltipDefinition>(StringComparer.OrdinalIgnoreCase)
            match includeTooltips with
            | Some map ->
                for kv in map do
                    merged.[kv.Key] <- kv.Value
            | None -> ()
            for kv in inlineTooltips do
                merged.[kv.Key] <- kv.Value
            match inspect with
            | Some { TooltipId = Some tooltipId } ->
                match merged.TryGetValue tooltipId with
                | true, resolved -> Some resolved
                | false, _ -> None
            | _ -> None

    let private validateCardFilterBinding
        (cardId: string)
        (boundFilters: IReadOnlyList<string>)
        (filters: IReadOnlyList<FilterDefinition>)
        =
        if boundFilters.Count = 0 then ()
        else
            let registry =
                filters
                |> Seq.map (fun f -> f.Name)
                |> Seq.distinct
                |> Seq.map (fun name -> name, name)
                |> dict
            for name in boundFilters do
                if String.Equals(name, CardBindResolver.dashboardToken, StringComparison.OrdinalIgnoreCase) then ()
                elif not (registry.ContainsKey name) then
                    raise (DashSpecParseException($"Card '{cardId}': bind references unknown filter '{name}'."))

    let rec private validateExtensionBlock (block: ExtensionBlockNode) (cardId: string) (parseOptions: DashSpecParseOptions) =
        if parseOptions.KnownActionHandlers.Count > 0 then
            match block.Properties.TryGetValue "action" with
            | true, action when not (parseOptions.KnownActionHandlers.Contains action) ->
                raise (DashSpecParseException($"Card '{cardId}': unknown action handler '{action}'."))
            | _ -> ()
        for nested in block.Nested do
            validateExtensionBlock nested cardId parseOptions

    let parse
        (reader: TokenReader)
        (filters: IReadOnlyList<FilterDefinition>)
        (specDirectory: string option)
        (includes: ModuleIncludeState option)
        (parseOptions: DashSpecParseOptions option)
        (phaseId: string option)
        (pageId: string option)
        =
        let parseOptions = defaultArg parseOptions DashSpecParseOptions.defaultOptions
        let id = reader.ReadIdent()
        let mutable title: string option = None
        if reader.TryKeywordSameLine "as" then title <- Some(reader.ReadString())
        let layoutRef = ParserUtilities.tryReadLayoutRef reader
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        let slotBuilder = CardDiagramSlotBuilder.create ()
        let diagram = ref None
        let dataSource = ref None
        let placement = ref None
        let mutable useCardPreset: string option = None
        let boundFilters = ResizeArray<string>()
        let localFilters = ResizeArray<string>()
        let mutable filterHostCardId: string option = None
        let hostedFilters = ResizeArray<string>()
        let legend = ref None
        let presentation = ref None
        let seriesTransform = ref None
        let interiorBoard = ref None
        let diagramSlotRef = ref None
        let clickBehaviour = ref None
        let visibility = ref None
        let matrixLimits = ref None
        let mutable oversizeMessage: string option = None
        let chrome = ref None
        let extensionBlocks = ResizeArray<ExtensionBlockNode>()
        let mutable localFiltersManualApply = false
        let mutable localFiltersApplySplitIndex : int option = None
        let mutable localFiltersChromeBoard : LayoutBoardDefinition option = None
        let includeFragment = ref SpecIncludeFragmentResolver.emptyFragment
        let inspect = ref None
        let tooltip = ref None
        let flowInput = ref None
        let inlineTooltips = Dictionary<string, TooltipDefinition>(StringComparer.OrdinalIgnoreCase)

        while not (BlockSyntax.isBlockEnd reader "card" (Some id)) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "card" (Some id) then ()
            elif reader.TryKeyword "title" then
                reader.Expect TokenKind.Eq
                title <- Some(reader.ReadString())
                reader.SkipNewlines()
            elif reader.TryKeyword "chrome" then
                if chrome.Value.IsSome then
                    raise (DashSpecParseException($"Card '{id}': duplicate chrome block."))
                chrome.Value <- Some(CardChromeParser.parse reader id)
                reader.SkipNewlines()
            elif reader.TryKeyword "when" then
                let whenTarget = reader.ReadIdent()
                if String.Equals(whenTarget, "oversize", StringComparison.OrdinalIgnoreCase) then
                    if oversizeMessage.IsSome then
                        raise (DashSpecParseException($"Card '{id}': duplicate when oversize block."))
                    oversizeMessage <- Some(CardVisibilityParser.parseOversizeWhen reader id)
                else
                    if visibility.Value.IsSome then
                        raise (DashSpecParseException($"Card '{id}': duplicate when block."))
                    visibility.Value <- Some(CardVisibilityParser.parseFilterWhen reader id whenTarget)
                reader.SkipNewlines()
            elif reader.TryKeyword "limits" then
                if matrixLimits.Value.IsSome then
                    raise (DashSpecParseException($"Card '{id}': duplicate limits block."))
                matrixLimits.Value <- Some(CardLimitsParser.parse reader id)
                reader.SkipNewlines()
            elif reader.TryKeyword "on" then
                if not (reader.TryKeyword "click") then
                    raise (DashSpecParseException($"Card '{id}': only 'on click' is supported in v1."))
                if clickBehaviour.Value.IsSome then
                    raise (DashSpecParseException($"Card '{id}': duplicate on click block."))
                clickBehaviour.Value <- Some(CardClickParser.parseClickBlock reader id parseOptions)
                reader.SkipNewlines()
            elif reader.TryKeyword "include" then
                match specDirectory with
                | None | Some (null | "") ->
                    raise (DashSpecParseException($"Card '{id}': include requires spec directory when parsing (path to the .dashspec folder)."))
                | Some dir ->
                    let kind, reference = DiagramModuleParser.readIncludeReference reader
                    includeFragment.Value <-
                        SpecIncludeFragmentResolver.merge includeFragment.Value (SpecIncludeFragmentResolver.load kind reference dir)
                reader.SkipNewlines()
            elif reader.TryKeyword "use" then
                useCardPreset <- Some(reader.ReadIdent())
                reader.SkipNewlines()
            elif reader.TryKeyword "data" then
                if reader.TryKeyword "for" then
                    let slotName = reader.ReadIdent()
                    if String.IsNullOrWhiteSpace slotName then
                        raise (DashSpecParseException($"Card '{id}': data for requires slot id."))
                    let ds, slotBind = parseDataBlockForSlot reader id specDirectory
                    CardDiagramSlotBuilder.applyData slotBuilder slotName ds slotBind
                else
                    parseDataBlock reader id specDirectory dataSource boundFilters
                    match dataSource.Value with
                    | Some ds ->
                        let slotRef = CardDiagramSlotBuilder.resolveSlotRef slotBuilder diagramSlotRef.Value
                        CardDiagramSlotBuilder.applyData slotBuilder slotRef ds (boundFilters :> IReadOnlyList<_>)
                    | None -> ()
                reader.SkipNewlines()
            elif reader.TryKeyword "view" then
                parseViewBlock reader id specDirectory includes parseOptions slotBuilder diagram diagramSlotRef legend presentation seriesTransform includeFragment
                reader.SkipNewlines()
            elif reader.TryKeyword "override" then
                CardDiagramOverrideParser.parseOverridesBlock reader id false diagram legend presentation seriesTransform
                reader.SkipNewlines()
            elif reader.TryKeyword "overrides" then
                CardDiagramOverrideParser.parseOverridesBlock reader id true diagram legend presentation seriesTransform
                reader.SkipNewlines()
            elif reader.TryKeyword "bind" then
                boundFilters.AddRange(parseBind reader)
                reader.SkipNewlines()
            elif reader.TryKeyword "filters" then
                if reader.TryKeywordSameLine "host" then
                    let hostCardId = reader.ReadIdent()
                    if filterHostCardId.IsSome then
                        raise (DashSpecParseException($"Card '{id}' declares more than one filters host block."))
                    filterHostCardId <- Some hostCardId
                    hostedFilters.AddRange(parseFilterPlacementList reader "filters" "filters host")
                elif reader.IsOnNewline()
                     || (reader.TryPeekIdent().IsSome
                         && String.Equals(reader.TryPeekIdent().Value, "apply", StringComparison.OrdinalIgnoreCase)) then
                    let names, manualApply, applySplit, chromeBoard = parseLocalFiltersBlock reader id
                    localFilters.AddRange(names)
                    localFiltersManualApply <- manualApply
                    localFiltersApplySplitIndex <- applySplit
                    localFiltersChromeBoard <- chromeBoard
                else
                    localFilters.AddRange(reader.ReadCommaListInline())
                reader.SkipNewlines()
            elif reader.TryKeyword "diagram" then
                let slotRef =
                    match ParserUtilities.tryReadLayoutRef reader with
                    | Some r ->
                        if diagramSlotRef.Value.IsNone then diagramSlotRef.Value <- Some r
                        r
                    | None -> CardDiagramSlotBuilder.DiagramSlotId
                parseDiagramStatement reader id includes "card" diagramSlotRef diagram legend presentation seriesTransform includeFragment
                match diagram.Value with
                | Some d ->
                    CardDiagramSlotBuilder.touchDiagram slotBuilder slotRef d legend.Value presentation.Value seriesTransform.Value
                | None -> ()
                reader.SkipNewlines()
            elif reader.TryKeyword "place" then
                placement.Value <- Some(LayoutParser.parsePlacement reader)
                reader.SkipNewlines()
            elif reader.TryKeyword "layout" then
                if reader.TryPeekIdent().IsSome && String.Equals(reader.TryPeekIdent().Value, "grid", StringComparison.OrdinalIgnoreCase) then
                    raise (DashSpecParseException($"Card '{id}': use module connect block for layout grid; card layout is a bracket board only."))
                let parsedPlacement, parsedBoard = parseCardLayoutContainer reader id
                if placement.Value.IsNone then placement.Value <- parsedPlacement
                if interiorBoard.Value.IsNone then interiorBoard.Value <- parsedBoard
                reader.SkipNewlines()
            elif reader.TryKeyword "input" then
                if flowInput.Value.IsSome then
                    raise (DashSpecParseException($"Card '{id}': only one flow input is supported in v1."))
                flowInput.Value <- Some(CardFlowInputParser.parse reader id)
                reader.SkipNewlines()
            elif reader.TryKeyword "datasource" then
                dataSource.Value <- Some(DataSourceParser.parse reader specDirectory)
                match dataSource.Value with
                | Some ds ->
                    let slotRef = CardDiagramSlotBuilder.resolveSlotRef slotBuilder diagramSlotRef.Value
                    CardDiagramSlotBuilder.applyData slotBuilder slotRef ds (boundFilters :> IReadOnlyList<_>)
                | None -> ()
                reader.SkipNewlines()
            elif reader.TryKeyword "legend" then
                legend.Value <- Some(parseLegend reader)
                reader.SkipNewlines()
            elif reader.TryKeyword "presentation" then
                presentation.Value <- Some(parsePresentation reader)
                reader.SkipNewlines()
            elif reader.TryKeyword "inspect" then
                inspect.Value <-
                    InspectPresentationParser.merge inspect.Value (Some(InspectPresentationParser.parse reader $"Card '{id}'"))
                reader.SkipNewlines()
            elif reader.TryKeyword "tooltip" then
                let tooltipId = reader.ReadIdent()
                if String.IsNullOrWhiteSpace tooltipId then
                    raise (DashSpecParseException($"Card '{id}': inline tooltip requires an id."))
                inlineTooltips.[tooltipId] <- TooltipModuleParser.parseInline reader tooltipId
                reader.SkipNewlines()
            elif reader.TryKeyword "transform" then
                if not (reader.TryKeyword "series") then
                    raise (DashSpecParseException("Expected 'series' after transform."))
                if reader.TryKeyword "max" then
                    reader.Expect TokenKind.Eq
                    let mutable max = 0
                    if not (Int32.TryParse(reader.ReadScalarValue(), &max) && max > 0) then
                        raise (DashSpecParseException($"Card '{id}': transform series max must be a positive integer."))
                    seriesTransform.Value <- Some({ UsePreset = None; Max = Some max; OtherLabel = None })
                else
                    reader.SkipNewlines()
                    seriesTransform.Value <- Some(parseSeriesTransform reader)
                reader.SkipNewlines()
            elif reader.TryKeyword "where" then
                raise (DashSpecParseException($"Card '{id}': 'where' is no longer used — list filters in bind …; Core compiles WHERE from bind (date/field → AND …, top → TOP/LIMIT)."))
            else
                match reader.TryPeekIdent() with
                | Some extensionKeyword when parseOptions.ExtensionBlockKeywords.Contains extensionKeyword ->
                    let block = ExtensionBlockParser.parse reader extensionKeyword parseOptions.ExtensionBlockKeywords
                    validateExtensionBlock block id parseOptions
                    extensionBlocks.Add(block)
                    reader.SkipNewlines()
                | _ -> raise (reader.Unexpected())

        BlockSyntax.expectBlockEnd reader "card" (Some id)

        match title with
        | None | Some (null | "") -> raise (DashSpecParseException($"Card '{id}' requires title (as \"…\" or title = \"…\")."))
        | _ -> ()

        if includeFragment.Value.Diagram.IsSome then
            diagram.Value <-
                match diagram.Value with
                | None -> includeFragment.Value.Diagram
                | Some current ->
                    (SpecIncludeFragmentResolver.merge
                        { SpecIncludeFragmentResolver.emptyFragment with Diagram = Some current }
                        { SpecIncludeFragmentResolver.emptyFragment with Diagram = includeFragment.Value.Diagram }).Diagram

        if includeFragment.Value.Presentation.IsSome then
            presentation.Value <-
                match presentation.Value with
                | None -> includeFragment.Value.Presentation
                | Some current ->
                    (SpecIncludeFragmentResolver.merge
                        { SpecIncludeFragmentResolver.emptyFragment with Presentation = Some current }
                        { SpecIncludeFragmentResolver.emptyFragment with Presentation = includeFragment.Value.Presentation }).Presentation

        if includeFragment.Value.SeriesTransform.IsSome then
            seriesTransform.Value <-
                match seriesTransform.Value with
                | None -> includeFragment.Value.SeriesTransform
                | Some current ->
                    (SpecIncludeFragmentResolver.merge
                        { SpecIncludeFragmentResolver.emptyFragment with SeriesTransform = includeFragment.Value.SeriesTransform }
                        { SpecIncludeFragmentResolver.emptyFragment with SeriesTransform = Some current }).SeriesTransform

        inspect.Value <- InspectPresentationParser.merge includeFragment.Value.Inspect inspect.Value
        tooltip.Value <-
            resolveCardTooltip inspect.Value inlineTooltips includeFragment.Value.Tooltips tooltip.Value

        if diagram.Value.IsNone && useCardPreset.IsNone then
            raise (DashSpecParseException("Card requires a diagram block or use <card-preset>."))

        if slotBuilder.Slots.Count = 0 then
            match diagram.Value, dataSource.Value with
            | Some d, Some ds ->
                let slotRef = CardDiagramSlotBuilder.resolveSlotRef slotBuilder diagramSlotRef.Value
                CardDiagramSlotBuilder.touchDiagram slotBuilder slotRef d legend.Value presentation.Value seriesTransform.Value
                CardDiagramSlotBuilder.applyData slotBuilder slotRef ds (boundFilters :> IReadOnlyList<_>)
            | _ when useCardPreset.IsSome -> ()
            | _ ->
                raise (DashSpecParseException($"Card '{id}': requires diagram and data blocks, diagram slots, or use <card-preset>."))
        else
            CardDiagramSlotBuilder.pruneDataOnlyDiagramSlot slotBuilder
            match diagram.Value with
            | Some d ->
                for slotRef in slotBuilder.Order do
                    let scratch = slotBuilder.Slots.[slotRef]
                    if scratch.Diagram.IsNone then
                        scratch.Diagram <- Some d
                        CardDiagramSlotBuilder.touchDiagram slotBuilder slotRef d legend.Value presentation.Value seriesTransform.Value
            | None -> ()

        for slotRef in slotBuilder.Order do
            let scratch = slotBuilder.Slots.[slotRef]
            if scratch.BoundFilters.Count = 0 && boundFilters.Count > 0 then
                scratch.BoundFilters.AddRange boundFilters
            if scratch.DataSource.IsNone && dataSource.Value.IsSome then
                scratch.DataSource <- dataSource.Value

        if flowInput.Value.IsSome then
            let flowDataPlaceholder =
                { Kind = DataSourceKind.View
                  Value = ""
                  SqlCarrier = None
                  Sheet = None
                  RowsType = ""
                  ProviderInfer = true }

            for slotRef in slotBuilder.Order do
                let scratch = slotBuilder.Slots.[slotRef]
                if scratch.Diagram.IsSome && scratch.DataSource.IsNone then
                    scratch.DataSource <- Some flowDataPlaceholder

            if dataSource.Value.IsNone then
                dataSource.Value <- Some flowDataPlaceholder

        let diagramSlotsFinal =
            match CardDiagramSlotBuilder.build slotBuilder id with
            | Some map -> map
            | None when useCardPreset.IsSome ->
                Dictionary<string, CardDiagramSlot>(StringComparer.OrdinalIgnoreCase) :> IReadOnlyDictionary<_, _>
            | None -> raise (DashSpecParseException($"Card '{id}': failed to build diagram slots."))

        for slot in diagramSlotsFinal.Values do
            validateCardFilterBinding id slot.BoundFilters filters

        validateCardFilterBinding id (boundFilters :> IReadOnlyList<_>) filters

        let presetDiagramPlaceholder =
            { Kind = ""
              Properties = Dictionary<string, string>() :> IReadOnlyDictionary<_, _>
              UsePreset = None }

        let presetDataSourcePlaceholder =
            { Kind = DataSourceKind.View
              Value = ""
              SqlCarrier = None
              Sheet = None
              RowsType = ""
              ProviderInfer = true }

        let primaryKey, primaryDiagram, primaryDataSource, primaryBound, primaryLegend, primaryPresentation, primarySeriesTransform =
            if diagramSlotsFinal.Count = 0 && useCardPreset.IsSome then
                (CardDiagramSlotBuilder.DiagramSlotId,
                 presetDiagramPlaceholder,
                 presetDataSourcePlaceholder,
                 boundFilters :> IReadOnlyList<_>,
                 legend.Value,
                 presentation.Value,
                 seriesTransform.Value)
            else
                let key = CardDiagramSlotBuilder.resolvePrimarySlotKey slotBuilder interiorBoard.Value
                let slot = diagramSlotsFinal.[key]
                (key, slot.Diagram, slot.DataSource, slot.BoundFilters, slot.Legend, slot.Presentation, slot.SeriesTransform)

        if dataSource.Value.IsNone && useCardPreset.IsNone then
            dataSource.Value <- Some primaryDataSource

        if dataSource.Value.IsNone && useCardPreset.IsNone && flowInput.Value.IsNone then
            raise (DashSpecParseException("Card requires a datasource block, flow input, or use <card-preset>."))

        { Id = id
          Title = title.Value
          Diagram = primaryDiagram
          DataSource = primaryDataSource
          BoundFilters = primaryBound
          LocalFilters = localFilters :> IReadOnlyList<_>
          Placement = placement.Value
          TabId = None
          LayoutRef = layoutRef
          UseCardPreset = useCardPreset
          Legend = legend.Value |> Option.orElse primaryLegend
          Presentation = presentation.Value |> Option.orElse primaryPresentation
          SeriesTransform = seriesTransform.Value |> Option.orElse primarySeriesTransform
          FilterHostCardId = filterHostCardId
          HostedFilters = Some(hostedFilters :> IReadOnlyList<_>)
          InteriorBoard = interiorBoard.Value
          DiagramSlotRef =
            if String.Equals(primaryKey, CardDiagramSlotBuilder.DiagramSlotId, StringComparison.Ordinal) then
                None
            else
                Some primaryKey
          DiagramSlots = diagramSlotsFinal
          ClickBehaviour = clickBehaviour.Value
          ExtensionBlocks = extensionBlocks :> IReadOnlyList<_>
          LocalFiltersManualApply = localFiltersManualApply
          LocalFiltersApplySplitIndex = localFiltersApplySplitIndex
          LocalFiltersChromeBoard = localFiltersChromeBoard
          Visibility = visibility.Value
          PhaseId = phaseId
          PageId = pageId
          MatrixLimits = matrixLimits.Value
          OversizeMessage = oversizeMessage
          Chrome = chrome.Value
          Inspect = inspect.Value
          Tooltip = tooltip.Value
          FlowInput = flowInput.Value }
