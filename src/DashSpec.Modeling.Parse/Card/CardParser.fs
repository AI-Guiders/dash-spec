namespace DashSpec.Modeling.Parse.Card

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.DataSource
open DashSpec.Modeling.Parse.Diagram
open DashSpec.Modeling.Parse.Filter
open DashSpec.Modeling.Parse.Import
open DashSpec.Modeling.Parse.Layout
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Presentation
open DashSpec.Modeling.Parse.Tooltip
open DashSpec.Modeling.Parse.Filter
open DashSpec.Modeling.Parse.Transform
open DashSpec.Modeling.Parse.Document

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
                raise (
                    DashSpecParseException(
                        $"Card '{cardId}': filter wiring belongs in flow (filter -> [toolbar] chrome.card.{cardId}); filters block is layout/apply only."
                    )
                )

        reader.SkipNewlines()
        BlockSyntax.expectBlockEnd reader "filters" None

        if chromeBoard.Value.IsNone && not manualApply then
            raise (
                DashSpecParseException(
                    $"Card '{cardId}': filters block is for layout/apply chrome only; wire filters in flow."
                )
            )

        names :> IReadOnlyList<_>, manualApply, applySplitIndex, chromeBoard.Value

    let private parseDiagramStatement
        (reader: TokenReader)
        (cardId: string)
        (includes: ModuleImportState option)
        (parentEndKind: string)
        (diagramSlotRef: string option ref)
        (diagram: DiagramDefinition option ref)
        (legend: LegendDefinition option ref)
        (presentation: PresentationBlock option ref)
        (seriesTransform: SeriesTransformBlock option ref)
        (includeFragment: UnitFragment ref)
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

            let mutable registered = UnitFragmentResolver.emptyFragment
            let mutable parsedKindRegistry = false

            match includes with
            | Some state when state.TryGetDiagram(diagramName, &registered) ->
                includeFragment.Value <- UnitFragmentResolver.merge includeFragment.Value registered
                if registered.Diagram.IsSome then
                    diagram.Value <-
                        match diagram.Value with
                        | None -> registered.Diagram
                        | Some current ->
                            (UnitFragmentResolver.merge
                                { UnitFragmentResolver.emptyFragment with Diagram = Some current }
                                { UnitFragmentResolver.emptyFragment with Diagram = registered.Diagram }).Diagram
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
                                (UnitFragmentResolver.merge
                                    { UnitFragmentResolver.emptyFragment with Diagram = Some current }
                                    { UnitFragmentResolver.emptyFragment with Diagram = inlineDelta.Diagram }).Diagram
                    if inlineDelta.Legend.IsSome then legend.Value <- inlineDelta.Legend
                    if inlineDelta.Presentation.IsSome then
                        presentation.Value <-
                            match presentation.Value with
                            | None -> inlineDelta.Presentation
                            | Some current ->
                                (UnitFragmentResolver.merge
                                    { UnitFragmentResolver.emptyFragment with Presentation = Some current }
                                    { UnitFragmentResolver.emptyFragment with Presentation = inlineDelta.Presentation }).Presentation
                    if inlineDelta.SeriesTransform.IsSome then
                        seriesTransform.Value <-
                            match seriesTransform.Value with
                            | None -> inlineDelta.SeriesTransform
                            | Some current ->
                                (UnitFragmentResolver.merge
                                    { UnitFragmentResolver.emptyFragment with SeriesTransform = Some current }
                                    { UnitFragmentResolver.emptyFragment with SeriesTransform = inlineDelta.SeriesTransform }).SeriesTransform

    /// ADR-0113: diagram for a switch-slot payload — imported diagram or preset id only
    /// (inline kind blocks and inline overrides stay a classic-slot feature).
    let private resolvePayloadDiagram
        (cardId: string)
        (includes: ModuleImportState option)
        (payloadName: string)
        (diagramName: string)
        =
        let mutable registered = UnitFragmentResolver.emptyFragment
        match includes with
        | Some state when state.TryGetDiagram(diagramName, &registered) ->
            match registered.Diagram with
            | Some diagram -> diagram
            | None ->
                raise (
                    DashSpecParseException(
                        $"Card '{cardId}': payload '{payloadName}' diagram '{diagramName}' resolves to a fragment without a diagram.")
                )
        | _ ->
            if DiagramKindRegistry.tryResolve diagramName |> fst then
                raise (
                    DashSpecParseException(
                        $"Card '{cardId}': payload '{payloadName}' must reference an imported diagram or preset id; got kind '{diagramName}'.")
                )
            else
                { Kind = ""
                  Properties = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) :> IReadOnlyDictionary<_, _>
                  UsePreset = Some diagramName }

    /// ADR-0113: `diagram ref <slot>` followed by `payload <name> <diagram>` lines —
    /// a switch slot rendering as one cell with one active payload.
    let private parsePayloadBlock
        (reader: TokenReader)
        (cardId: string)
        (includes: ModuleImportState option)
        (slotBuilder: CardDiagramSlotBuilder.Builder)
        (slotRef: string)
        =
        let mutable count = 0
        while reader.TryKeyword "payload" do
            let payloadName = reader.ReadIdentSameLine()
            if String.IsNullOrWhiteSpace payloadName then
                raise (reader.Unexpected("payload name"))
            let diagramName = reader.ReadIdentSameLine()
            if String.IsNullOrWhiteSpace diagramName then
                raise (reader.Unexpected("payload diagram id"))
            let diagram = resolvePayloadDiagram cardId includes payloadName diagramName
            CardDiagramSlotBuilder.addPayload slotBuilder slotRef payloadName diagram cardId
            count <- count + 1
            reader.SkipNewlines()
        if count = 0 then
            raise (
                DashSpecParseException(
                    $"Card '{cardId}': diagram ref {slotRef} requires a diagram name on the same line or at least one 'payload <name> <diagram>' line.")
            )

    let private parseViewBlock
        (reader: TokenReader)
        (cardId: string)
        (specDirectory: string option)
        (includes: ModuleImportState option)
        (parseOptions: DashSpecParseOptions)
        (slotBuilder: CardDiagramSlotBuilder.Builder)
        (diagram: DiagramDefinition option ref)
        (diagramSlotRef: string option ref)
        (legend: LegendDefinition option ref)
        (presentation: PresentationBlock option ref)
        (seriesTransform: SeriesTransformBlock option ref)
        (includeFragment: UnitFragment ref)
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
        (includes: ModuleImportState option)
        (parseOptions: DashSpecParseOptions option)
        (phaseId: string option)
        (pageId: string option)
        =
        let parseOptions = defaultArg parseOptions DashSpecParseOptions.defaultOptions
        let id = reader.ReadIdent()
        let title = ref (None: string option)
        if reader.TryKeywordSameLine "as" then title.Value <- Some(reader.ReadString())
        let layoutRef = ParserUtilities.tryReadLayoutRef reader
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        let slotBuilder = CardDiagramSlotBuilder.create ()
        let diagram = ref None
        let dataSource = ref None
        let placement = ref None
        let useCardPreset = ref (None: string option)
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
        let clickHandlers = ResizeArray<CardClickHandler>()
        let genericClickDeclared = ref false
        let visibility = ref None
        let matrixLimits = ref None
        let oversizeMessage = ref (None: string option)
        let chrome = ref None
        let extensionBlocks = ResizeArray<ExtensionBlockNode>()
        let localFiltersManualApply = ref false
        let localFiltersApplySplitIndex = ref (None: int option)
        let localFiltersChromeBoard = ref (None: LayoutBoardDefinition option)
        let includeFragment = ref UnitFragmentResolver.emptyFragment
        let inspect = ref None
        let tooltip = ref None
        let flowInput = ref None
        let cardInputs = Dictionary<string, CardFlowInput>(StringComparer.OrdinalIgnoreCase)
        let interiorFlowBuilder = CardInteriorFlowParser.createBuilder ()
        let inlineTooltips = Dictionary<string, TooltipDefinition>(StringComparer.OrdinalIgnoreCase)

        let onTitle () =
            reader.Expect TokenKind.Eq
            title.Value <- Some(reader.ReadString())
            reader.SkipNewlines()

        let onChrome () =
            if chrome.Value.IsSome then
                raise (DashSpecParseException($"Card '{id}': duplicate chrome block."))
            chrome.Value <- Some(CardChromeParser.parse reader id)
            reader.SkipNewlines()

        let onWhen () =
            let whenTarget = reader.ReadIdent()
            if String.Equals(whenTarget, "oversize", StringComparison.OrdinalIgnoreCase) then
                if oversizeMessage.Value.IsSome then
                    raise (DashSpecParseException($"Card '{id}': duplicate when oversize block."))
                oversizeMessage.Value <- Some(CardVisibilityParser.parseOversizeWhen reader id)
            else
                if visibility.Value.IsSome then
                    raise (DashSpecParseException($"Card '{id}': duplicate when block."))
                visibility.Value <- Some(CardVisibilityParser.parseFilterWhen reader id whenTarget)
            reader.SkipNewlines()

        let onLimits () =
            if matrixLimits.Value.IsSome then
                raise (DashSpecParseException($"Card '{id}': duplicate limits block."))
            matrixLimits.Value <- Some(CardLimitsParser.parse reader id)
            reader.SkipNewlines()

        let addClickHandler (handler: CardClickHandler) =
            if
                clickHandlers
                |> Seq.exists (fun existing -> String.Equals(existing.Source, handler.Source, StringComparison.OrdinalIgnoreCase))
            then
                raise (DashSpecParseException($"Card '{id}': duplicate on {handler.Source} click handler."))

            clickHandlers.Add(handler)

        let onOn () =
            // ADR-0107: `on click …` (generic: pipe-table form or legacy effect statements)
            // or `on <source> click …` (a sourced handler; the source vocabulary is
            // plugin-declared, validated like action handlers).
            let sourceWords = ResizeArray<string>()
            let mutable reachedClick = false

            while not reachedClick do
                if reader.TryKeyword "click" then
                    reachedClick <- true
                else
                    match reader.TryPeekIdent() with
                    | Some word ->
                        sourceWords.Add(word.Trim())
                        reader.ReadIdent() |> ignore
                    | None ->
                        raise (DashSpecParseException($"Card '{id}': 'on' requires 'click' or '<source> click'."))

            if sourceWords.Count = 0 then
                if genericClickDeclared.Value then
                    raise (DashSpecParseException($"Card '{id}': duplicate on click block."))

                genericClickDeclared.Value <- true

                if reader.CurrentKind = TokenKind.TableRow then
                    let rows = BlockGrammar.readPipeTableRows reader "click" $"Card '{id}': on click"
                    CardClickParser.applyClickTableRows id parseOptions.KnownClickSources addClickHandler (List.ofSeq rows)
                else
                    clickBehaviour.Value <- Some(CardClickParser.parseClickBlock reader id parseOptions)
            else
                let source = (String.Join(" ", sourceWords)).Trim().ToLowerInvariant()

                if
                    parseOptions.KnownClickSources.Count > 0
                    && not (parseOptions.KnownClickSources.Contains source)
                then
                    let declaredList = String.Join(", ", parseOptions.KnownClickSources)
                    raise (
                        DashSpecParseException(
                            $"Card '{id}': unknown click source '{source}' (declared: {declaredList}).")
                    )

                let behaviour = CardClickParser.parseClickBlock reader id parseOptions
                addClickHandler { Source = source; Effects = behaviour.Effects }

            reader.SkipNewlines()

        let onUse () =
            useCardPreset.Value <- Some(reader.ReadIdent())
            reader.SkipNewlines()

        let onView () =
            parseViewBlock reader id specDirectory includes parseOptions slotBuilder diagram diagramSlotRef legend presentation seriesTransform includeFragment
            reader.SkipNewlines()

        let onOverride () =
            CardDiagramOverrideParser.parseOverridesBlock reader id false diagram legend presentation seriesTransform
            reader.SkipNewlines()

        let onOverrides () =
            CardDiagramOverrideParser.parseOverridesBlock reader id true diagram legend presentation seriesTransform
            reader.SkipNewlines()

        let onBind () =
            boundFilters.AddRange(parseBind reader)
            reader.SkipNewlines()

        let onFilters () =
            if reader.TryKeywordSameLine "host" then
                raise (
                    DashSpecParseException(
                        $"Card '{id}': filters host removed; use page/report flow: hostCard.filter -> host.chrome.card.<consumer>."
                    )
                )
            elif reader.IsOnNewline()
                 || (reader.TryPeekIdent().IsSome
                     && String.Equals(reader.TryPeekIdent().Value, "apply", StringComparison.OrdinalIgnoreCase)) then
                let names, manualApply, applySplit, chromeBoard = parseLocalFiltersBlock reader id
                localFilters.AddRange(names)
                localFiltersManualApply.Value <- manualApply
                localFiltersApplySplitIndex.Value <- applySplit
                localFiltersChromeBoard.Value <- chromeBoard
            else
                raise (
                    DashSpecParseException(
                        $"Card '{id}': inline filters list removed; use flow: filter -> [toolbar] chrome.card.{id}."
                    )
                )

            reader.SkipNewlines()

        let onDiagram () =
            let slotRef =
                match ParserUtilities.tryReadLayoutRef reader with
                | Some r ->
                    if diagramSlotRef.Value.IsNone then diagramSlotRef.Value <- Some r
                    r
                | None -> CardDiagramSlotBuilder.DiagramSlotId
            // ADR-0113: `payload` on the following lines declares a switch slot.
            match reader.TryPeekIdent() with
            | Some next when String.Equals(next, "payload", StringComparison.OrdinalIgnoreCase) ->
                parsePayloadBlock reader id includes slotBuilder slotRef
            | _ ->
                parseDiagramStatement reader id includes "card" diagramSlotRef diagram legend presentation seriesTransform includeFragment
                match diagram.Value with
                | Some d ->
                    CardDiagramSlotBuilder.touchDiagram slotBuilder slotRef d legend.Value presentation.Value seriesTransform.Value
                | None -> ()
            reader.SkipNewlines()

        let onPlace () =
            placement.Value <- Some(LayoutParser.parsePlacement reader)
            reader.SkipNewlines()

        let onLayout () =
            if reader.TryPeekIdent().IsSome && String.Equals(reader.TryPeekIdent().Value, "grid", StringComparison.OrdinalIgnoreCase) then
                raise (DashSpecParseException($"Card '{id}': use module connect block for layout grid; card layout is a bracket board only."))
            let parsedPlacement, parsedBoard = parseCardLayoutContainer reader id
            if placement.Value.IsNone then placement.Value <- parsedPlacement
            if interiorBoard.Value.IsNone then interiorBoard.Value <- parsedBoard
            reader.SkipNewlines()

        let onLegend () =
            legend.Value <- Some(parseLegend reader)
            reader.SkipNewlines()

        let onPresentation () =
            presentation.Value <- Some(parsePresentation reader)
            reader.SkipNewlines()

        let onInspect () =
            inspect.Value <-
                InspectPresentationParser.merge inspect.Value (Some(InspectPresentationParser.parse reader $"Card '{id}'"))
            reader.SkipNewlines()

        let onTooltip () =
            let tooltipId = reader.ReadIdent()
            if String.IsNullOrWhiteSpace tooltipId then
                raise (DashSpecParseException($"Card '{id}': inline tooltip requires an id."))
            inlineTooltips.[tooltipId] <- TooltipModuleParser.parseInline reader tooltipId
            reader.SkipNewlines()

        let onTransform () =
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

        let onWhere () : unit =
            raise (DashSpecParseException($"Card '{id}': 'where' is no longer used — list filters in bind …; Core compiles WHERE from bind (date/field → AND …, top → TOP/LIMIT)."))

        let members: BlockGrammar.ContainerMember list =
            [ BlockGrammar.ChildKeyword("title", fun _ -> onTitle ())
              BlockGrammar.ChildKeyword("chrome", fun _ -> onChrome ())
              BlockGrammar.ChildKeyword("when", fun _ -> onWhen ())
              BlockGrammar.ChildKeyword("limits", fun _ -> onLimits ())
              BlockGrammar.ChildKeyword("on", fun _ -> onOn ())
              BlockGrammar.ChildKeyword("use", fun _ -> onUse ())
              BlockGrammar.ChildKeyword("view", fun _ -> onView ())
              BlockGrammar.ChildKeyword("override", fun _ -> onOverride ())
              BlockGrammar.ChildKeyword("overrides", fun _ -> onOverrides ())
              BlockGrammar.ChildKeyword("bind", fun _ -> onBind ())
              BlockGrammar.ChildKeyword("filters", fun _ -> onFilters ())
              BlockGrammar.ChildKeyword("diagram", fun _ -> onDiagram ())
              BlockGrammar.ChildKeyword("place", fun _ -> onPlace ())
              BlockGrammar.ChildKeyword("layout", fun _ -> onLayout ())
              BlockGrammar.ChildKeyword("legend", fun _ -> onLegend ())
              BlockGrammar.ChildKeyword("presentation", fun _ -> onPresentation ())
              BlockGrammar.ChildKeyword("inspect", fun _ -> onInspect ())
              BlockGrammar.ChildKeyword("tooltip", fun _ -> onTooltip ())
              BlockGrammar.ChildKeyword("transform", fun _ -> onTransform ())
              BlockGrammar.ChildKeyword("where", fun _ -> onWhere ()) ]

        while not (BlockSyntax.isBlockEnd reader "card" (Some id)) && not reader.IsEof do
            reader.SkipNewlines()
            ModuleImportGuard.rejectModuleImportInBody reader $"Card '{id}'"
            if BlockSyntax.isBlockEnd reader "card" (Some id) then ()
            elif BlockGrammar.tryDispatchChild reader members then ()
            elif CardInteriorFlowParser.tryAddQualifiedBlock reader interiorFlowBuilder id then
                reader.SkipNewlines()
            else
                match reader.TryPeekIdent() with
                | Some extensionKeyword when parseOptions.ExtensionBlockKeywords.Contains extensionKeyword ->
                    let block = ExtensionBlockParser.parse reader extensionKeyword parseOptions.ExtensionBlockKeywords
                    validateExtensionBlock block id parseOptions
                    extensionBlocks.Add(block)
                    reader.SkipNewlines()
                | _ -> raise (reader.Unexpected())

        BlockSyntax.expectBlockEnd reader "card" (Some id)

        let title = title.Value
        let useCardPreset = useCardPreset.Value
        let oversizeMessage = oversizeMessage.Value
        let localFiltersManualApply = localFiltersManualApply.Value
        let localFiltersApplySplitIndex = localFiltersApplySplitIndex.Value
        let localFiltersChromeBoard = localFiltersChromeBoard.Value

        match title with
        | None | Some (null | "") -> raise (DashSpecParseException($"Card '{id}' requires title (as \"…\" or title = \"…\")."))
        | _ -> ()

        if includeFragment.Value.Diagram.IsSome then
            diagram.Value <-
                match diagram.Value with
                | None -> includeFragment.Value.Diagram
                | Some current ->
                    (UnitFragmentResolver.merge
                        { UnitFragmentResolver.emptyFragment with Diagram = Some current }
                        { UnitFragmentResolver.emptyFragment with Diagram = includeFragment.Value.Diagram }).Diagram

        if includeFragment.Value.Presentation.IsSome then
            presentation.Value <-
                match presentation.Value with
                | None -> includeFragment.Value.Presentation
                | Some current ->
                    (UnitFragmentResolver.merge
                        { UnitFragmentResolver.emptyFragment with Presentation = Some current }
                        { UnitFragmentResolver.emptyFragment with Presentation = includeFragment.Value.Presentation }).Presentation

        if includeFragment.Value.SeriesTransform.IsSome then
            seriesTransform.Value <-
                match seriesTransform.Value with
                | None -> includeFragment.Value.SeriesTransform
                | Some current ->
                    (UnitFragmentResolver.merge
                        { UnitFragmentResolver.emptyFragment with SeriesTransform = includeFragment.Value.SeriesTransform }
                        { UnitFragmentResolver.emptyFragment with SeriesTransform = Some current }).SeriesTransform

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

        let interiorFlow = CardInteriorFlowParser.toDefinition interiorFlowBuilder

        if cardInputs.Count > 0 || interiorFlow.IsSome then
            CardInteriorFlowResolver.applyToBuilder
                slotBuilder
                id
                filters
                interiorFlow
                (cardInputs :> IReadOnlyDictionary<_, _>)

        let diagramSlotsFinal =
            match CardDiagramSlotBuilder.build slotBuilder id flowInput.Value with
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
              ProviderInfer = true
              ProviderId = None }

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

        let anySlotFlowInput =
            diagramSlotsFinal.Values |> Seq.exists (fun slot -> slot.FlowInput.IsSome)

        if dataSource.Value.IsNone
           && useCardPreset.IsNone
           && flowInput.Value.IsNone
           && cardInputs.Count = 0
           && not anySlotFlowInput then
            raise (DashSpecParseException("Card requires data flow wiring, report/page data flow, or use <card-preset>."))

        let resolvedCardFlowInput =
            match flowInput.Value with
            | Some fi -> Some fi
            | None ->
                let primaryKey = CardDiagramSlotBuilder.resolvePrimarySlotKey slotBuilder interiorBoard.Value
                if diagramSlotsFinal.ContainsKey primaryKey then
                    diagramSlotsFinal.[primaryKey].FlowInput
                else
                    None

        // ADR-0107: a named drill target must be a declared slot of this card.
        // ADR-0113: a switch-slot target addresses a payload — `drill <slot>.<payload>`;
        // a bare slot name is ambiguous when the slot carries payloads.
        let allClickEffects =
            seq {
                match clickBehaviour.Value with
                | Some behaviour -> yield! behaviour.Effects
                | None -> ()

                yield! clickHandlers |> Seq.collect (fun handler -> handler.Effects)
            }

        for effect in allClickEffects do
            match effect.TryGetDrillTarget with
            | Some target when not (String.IsNullOrWhiteSpace target) ->
                let parts = target.Split('.')
                let slotPart = parts.[0].Trim()
                if not (diagramSlotsFinal.ContainsKey slotPart) then
                    raise (
                        DashSpecParseException(
                            $"Card '{id}': drill table target slot '{target}' is not declared by this card (diagram ref {slotPart} <name>).")
                    )
                elif parts.Length > 2 then
                    raise (
                        DashSpecParseException($"Card '{id}': drill target '{target}' must be '<slot>' or '<slot>.<payload>'.")
                    )
                elif parts.Length = 2 then
                    let payloadPart = parts.[1].Trim()
                    let payloads = diagramSlotsFinal.[slotPart].Payloads
                    if payloads.Count = 0 then
                        raise (
                            DashSpecParseException(
                                $"Card '{id}': drill target '{target}' names payload '{payloadPart}' but slot '{slotPart}' has no payloads.")
                        )
                    elif
                        not (
                            payloads
                            |> Seq.exists (fun p -> String.Equals(p.Name, payloadPart, StringComparison.OrdinalIgnoreCase))
                        )
                    then
                        let declared = String.Join(", ", payloads |> Seq.map (fun p -> p.Name))
                        raise (
                            DashSpecParseException(
                                $"Card '{id}': drill target '{target}' names an unknown payload (declared: {declared}).")
                        )
                elif diagramSlotsFinal.[slotPart].Payloads.Count > 0 then
                    raise (
                        DashSpecParseException(
                            $"Card '{id}': drill target '{slotPart}' is a switch slot — address a payload: '{slotPart}.<payload>'.")
                    )
            | _ -> ()

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
          ClickHandlers = clickHandlers :> IReadOnlyList<_>
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
          FlowInput = resolvedCardFlowInput
          CardInputs = cardInputs :> IReadOnlyDictionary<_, _>
          InteriorFlow = interiorFlow }
