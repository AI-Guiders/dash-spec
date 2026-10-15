namespace DashSpec.Modeling.Parse.Import

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Diagram
open DashSpec.Modeling.Parse.Presentation
open DashSpec.Modeling.Parse.Tooltip
open DashSpec.Modeling.Parse.Transform

/// Resolved diagram / presentation / transform / tooltip fragment of one unit file.
[<CLIMutable>]
type UnitFragment =
    { Diagram: DiagramDefinition option
      Presentation: PresentationBlock option
      SeriesTransform: SeriesTransformBlock option
      Tooltips: IReadOnlyDictionary<string, TooltipDefinition> option
      Inspect: InspectPresentation option }

module UnitFragmentResolver =

    let emptyFragment =
        { Diagram = None
          Presentation = None
          SeriesTransform = None
          Tooltips = None
          Inspect = None }

    let private mergeDiagram (left: DiagramDefinition option) (right: DiagramDefinition option) =
        match right with
        | None -> left
        | Some r ->
            match left with
            | None -> Some r
            | Some l when String.IsNullOrWhiteSpace l.Kind && l.UsePreset.IsNone -> Some r
            | Some l ->
                let props = Dictionary<string, string>(l.Properties, StringComparer.OrdinalIgnoreCase)
                for kv in r.Properties do
                    props.[kv.Key] <- kv.Value
                let kind = if String.IsNullOrWhiteSpace r.Kind then l.Kind else r.Kind
                let preset = r.UsePreset |> Option.orElse l.UsePreset
                Some { Kind = kind; Properties = props :> IReadOnlyDictionary<_, _>; UsePreset = preset }

    let private mergePresentation (left: PresentationBlock option) (right: PresentationBlock option) =
        match right with
        | None -> left
        | Some r ->
            match left with
            | None -> Some r
            | Some l ->
                let inlineProps = Dictionary<string, string>(l.Properties, StringComparer.OrdinalIgnoreCase)
                for kv in r.Properties do
                    inlineProps.[kv.Key] <- kv.Value
                Some { UsePreset = r.UsePreset |> Option.orElse l.UsePreset; Properties = inlineProps :> IReadOnlyDictionary<_, _> }

    let private mergeSeriesTransform (left: SeriesTransformBlock option) (right: SeriesTransformBlock option) =
        match right with
        | None -> left
        | Some r ->
            match left with
            | None -> Some r
            | Some l ->
                Some
                    { UsePreset = r.UsePreset |> Option.orElse l.UsePreset
                      Max = r.Max |> Option.orElse l.Max
                      OtherLabel = r.OtherLabel |> Option.orElse l.OtherLabel }

    let private mergeTooltips
        (left: IReadOnlyDictionary<string, TooltipDefinition> option)
        (right: IReadOnlyDictionary<string, TooltipDefinition> option)
        =
        match right with
        | None -> left
        | Some r when r.Count = 0 -> left
        | Some r ->
            match left with
            | None -> Some r
            | Some l when l.Count = 0 -> Some r
            | Some l ->
                let merged = Dictionary<string, TooltipDefinition>(l, StringComparer.OrdinalIgnoreCase)
                for kv in r do
                    merged.[kv.Key] <- kv.Value
                Some(merged :> IReadOnlyDictionary<_, _>)

    let merge (current: UnitFragment) (incoming: UnitFragment) =
        { Diagram = mergeDiagram current.Diagram incoming.Diagram
          Presentation = mergePresentation current.Presentation incoming.Presentation
          SeriesTransform = mergeSeriesTransform current.SeriesTransform incoming.SeriesTransform
          Tooltips = mergeTooltips current.Tooltips incoming.Tooltips
          Inspect = InspectPresentationParser.merge current.Inspect incoming.Inspect }

    let private foldDiagramStatements (statements: IReadOnlyList<DiagramFragmentStatement>) =
        let mutable fragment = emptyFragment
        let mutable hasDiagram = false

        for statement in statements do
            match statement with
            | DiagramStatement diagram ->
                hasDiagram <- true
                fragment <- merge fragment { emptyFragment with Diagram = Some diagram }
            | PresentationStatement presentation ->
                fragment <- merge fragment { emptyFragment with Presentation = Some presentation }
            | SeriesTransformStatement transform ->
                fragment <- merge fragment { emptyFragment with SeriesTransform = Some transform }
            | TooltipStatement(id, definition) ->
                let map = Dictionary<string, TooltipDefinition>(StringComparer.OrdinalIgnoreCase)
                map.[id] <- definition
                fragment <- merge fragment { emptyFragment with Tooltips = Some(map :> IReadOnlyDictionary<_, _>) }
            | InspectStatement inspect ->
                fragment <- merge fragment { emptyFragment with Inspect = Some inspect }

        if not hasDiagram && fragment.Diagram.IsNone then
            raise (DashSpecParseException("Diagram module requires a chart kind block (e.g. heatmap … end heatmap)."))

        fragment

    let foldDiagramModule (text: string) : UnitFragment =
        foldDiagramStatements (DiagramModuleParser.parseDiagramModule text).Statements

    let foldDiagramModuleWithId (text: string) : string * UnitFragment =
        let id, doc = DiagramModuleParser.parseDiagramModuleWithId text
        id, foldDiagramStatements doc.Statements

    let parsePresentationBlock (text: string) : PresentationBlock =
        (PresentationModuleParser.parsePresentationModule text).Local
        |> Option.defaultWith (fun () -> raise (DashSpecParseException("@presentation module requires at least one property.")))
