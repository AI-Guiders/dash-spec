namespace DashSpec.Modeling.Parse.Card

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Diagram
open DashSpec.Modeling.Parse.Include
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Presentation
open DashSpec.Modeling.Parse.Transform

module CardDiagramOverrideParser =

    type DiagramDelta =
        { Diagram: DiagramDefinition option
          Legend: LegendDefinition option
          Presentation: PresentationBlock option
          SeriesTransform: SeriesTransformBlock option }

    let private toPresentationBlock (props: IDictionary<string, string>) =
        let usePreset =
            match props.TryGetValue "use" with
            | true, value -> Some value
            | false, _ -> None
        let inlineProps =
            let map = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            for kv in props do
                if not (kv.Key.Equals("use", StringComparison.OrdinalIgnoreCase)) then
                    map.[kv.Key] <- kv.Value
            map :> IReadOnlyDictionary<_, _>
        { UsePreset = usePreset; Properties = inlineProps }

    let private propValue (props: IDictionary<string, string>) key =
        match props.TryGetValue key with
        | true, value -> Some value
        | false, _ -> None

    let private parseLegendOverride (reader: TokenReader) =
        let props =
            PropertyBlockParser.parseWithEndKind reader PropertySchemas.legend "legend" "legend" false false
        { MinLabel = propValue props "min"
          MaxLabel = propValue props "max"
          Title = propValue props "title" }

    let private parsePresentationOverride (reader: TokenReader) =
        let props =
            PropertyBlockParser.parseWithEndKind reader PropertySchemas.presentation "presentation" "presentation" false false
        toPresentationBlock props

    let private parseSeriesOverride (reader: TokenReader) (cardId: string) (diagramId: string) =
        if reader.TryKeyword "max" then
            reader.Expect TokenKind.Eq
            let mutable max = 0
            if not (Int32.TryParse(reader.ReadScalarValue(), &max) && max > 0) then
                raise (DashSpecParseException($"Card '{cardId}': override for '{diagramId}' series max must be a positive integer."))
            { UsePreset = None; Max = Some max; OtherLabel = None }
        elif reader.IsOnNewline() then
            reader.SkipNewlines()
            let props =
                PropertyBlockParser.parseWithEndKind reader PropertySchemas.seriesTransform "series" "series" false false
            let usePreset = propValue props "use"
            let max =
                match propValue props "max" with
                | Some raw ->
                    let mutable parsed = 0
                    if Int32.TryParse(raw, &parsed) && parsed > 0 then Some parsed else None
                | None -> None
            let other = propValue props "other"
            { UsePreset = usePreset; Max = max; OtherLabel = other }
        else
            raise (reader.Unexpected "max or multiline series block")

    let private parseOverrideBody (reader: TokenReader) (diagramId: string) (cardId: string) (endKind: string) (endId: string option) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let props = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        let mutable legend: LegendDefinition option = None
        let mutable presentation: PresentationBlock option = None
        let mutable seriesTransform: SeriesTransformBlock option = None

        while not (BlockSyntax.isBlockEnd reader endKind endId) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader endKind endId then ()
            elif reader.TryKeyword "series" then
                seriesTransform <- Some(parseSeriesOverride reader cardId diagramId)
            elif reader.TryKeyword "legend" then
                legend <- Some(parseLegendOverride reader)
            elif reader.TryKeyword "presentation" then
                presentation <- Some(parsePresentationOverride reader)
            elif reader.TryPeekIdent().IsSome && reader.RawKind = TokenKind.Ident then
                let key = reader.ReadIdent()
                reader.Expect TokenKind.Eq
                props.[key] <- reader.ReadScalarValue()
                reader.SkipNewlines()
            else
                raise (reader.Unexpected())

        BlockSyntax.expectBlockEnd reader endKind endId

        let diagram =
            if props.Count > 0 then
                Some { Kind = ""; Properties = props :> IReadOnlyDictionary<_, _>; UsePreset = Some diagramId }
            else
                None

        { Diagram = diagram; Legend = legend; Presentation = presentation; SeriesTransform = seriesTransform }

    let private applyDelta
        (diagram: DiagramDefinition option ref)
        (legend: LegendDefinition option ref)
        (presentation: PresentationBlock option ref)
        (seriesTransform: SeriesTransformBlock option ref)
        (delta: DiagramDelta)
        (targetId: string)
        (cardId: string)
        =
        match diagram.Value with
        | Some d when d.UsePreset.IsSome && not (String.Equals(d.UsePreset.Value, targetId, StringComparison.OrdinalIgnoreCase)) ->
            raise (DashSpecParseException($"Card '{cardId}': override for '{targetId}' does not match view diagram '{d.UsePreset.Value}'."))
        | _ -> ()

        if delta.Diagram.IsSome then
            diagram.Value <-
                match diagram.Value with
                | None -> delta.Diagram
                | Some current ->
                    (SpecIncludeFragmentResolver.merge
                        { SpecIncludeFragmentResolver.emptyFragment with Diagram = Some current }
                        { SpecIncludeFragmentResolver.emptyFragment with Diagram = delta.Diagram }).Diagram

        if delta.Legend.IsSome then legend.Value <- delta.Legend
        if delta.Presentation.IsSome then
            presentation.Value <-
                match presentation.Value with
                | None -> delta.Presentation
                | Some current ->
                    (SpecIncludeFragmentResolver.merge
                        { SpecIncludeFragmentResolver.emptyFragment with Presentation = Some current }
                        { SpecIncludeFragmentResolver.emptyFragment with Presentation = delta.Presentation }).Presentation
        if delta.SeriesTransform.IsSome then
            seriesTransform.Value <-
                match seriesTransform.Value with
                | None -> delta.SeriesTransform
                | Some current ->
                    (SpecIncludeFragmentResolver.merge
                        { SpecIncludeFragmentResolver.emptyFragment with SeriesTransform = Some current }
                        { SpecIncludeFragmentResolver.emptyFragment with SeriesTransform = delta.SeriesTransform }).SeriesTransform

    let parseOverridesBlock
        (reader: TokenReader)
        (cardId: string)
        (plural: bool)
        (diagram: DiagramDefinition option ref)
        (legend: LegendDefinition option ref)
        (presentation: PresentationBlock option ref)
        (seriesTransform: SeriesTransformBlock option ref)
        =
        if plural then
            BlockSyntax.beginBlock reader
            reader.SkipNewlines()
            while not (BlockSyntax.isBlockEnd reader "overrides" None) && not reader.IsEof do
                reader.SkipNewlines()
                if BlockSyntax.isBlockEnd reader "overrides" None then ()
                elif not (reader.TryKeyword "for") then
                    raise (reader.Unexpected "for <diagram_id>")
                else
                    let targetId = reader.ReadIdent()
                    let delta = parseOverrideBody reader targetId cardId "for" None
                    applyDelta diagram legend presentation seriesTransform delta targetId cardId
            BlockSyntax.expectBlockEnd reader "overrides" None
        else
            if not (reader.TryKeyword "for") then raise (reader.Unexpected "for <diagram_id>")
            let diagramId = reader.ReadIdent()
            let delta = parseOverrideBody reader diagramId cardId "override" None
            applyDelta diagram legend presentation seriesTransform delta diagramId cardId

    let tryParseDiagramInlineBody (reader: TokenReader) (diagramId: string) (cardId: string) (parentEndKind: string) =
        reader.SkipNewlines()
        if BlockSyntax.isBlockEnd reader parentEndKind None || reader.IsEof then None
        elif
            match reader.TryPeekIdent() with
            | Some next ->
                String.Equals(next, "diagram", StringComparison.OrdinalIgnoreCase)
                || String.Equals(next, "legend", StringComparison.OrdinalIgnoreCase)
                || String.Equals(next, "datasource", StringComparison.OrdinalIgnoreCase)
                || String.Equals(next, "bind", StringComparison.OrdinalIgnoreCase)
                || String.Equals(next, "chrome", StringComparison.OrdinalIgnoreCase)
                || String.Equals(next, "when", StringComparison.OrdinalIgnoreCase)
                || String.Equals(next, "layout", StringComparison.OrdinalIgnoreCase)
                || String.Equals(next, "override", StringComparison.OrdinalIgnoreCase)
                || String.Equals(next, "data", StringComparison.OrdinalIgnoreCase)
                || String.Equals(next, "view", StringComparison.OrdinalIgnoreCase)
            | None -> false
        then
            None
        else
            Some(parseOverrideBody reader diagramId cardId "diagram" (Some diagramId))
