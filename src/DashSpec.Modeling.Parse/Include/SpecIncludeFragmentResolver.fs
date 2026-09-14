namespace DashSpec.Modeling.Parse.Include

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Diagram
open DashSpec.Modeling.Parse.Presentation
open DashSpec.Modeling.Parse.Tooltip
open DashSpec.Modeling.Parse.Transform

module SpecIncludeFragmentResolver =

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

    let merge (current: SpecIncludeFragment) (incoming: SpecIncludeFragment) =
        { Diagram = mergeDiagram current.Diagram incoming.Diagram
          Presentation = mergePresentation current.Presentation incoming.Presentation
          SeriesTransform = mergeSeriesTransform current.SeriesTransform incoming.SeriesTransform
          Tooltips = mergeTooltips current.Tooltips incoming.Tooltips
          Inspect = InspectPresentationParser.merge current.Inspect incoming.Inspect }

    let private resolveExistingFile (path: string) (includeKind: string) =
        if File.Exists path then path
        else
            let extensions =
                match includeKind.ToLowerInvariant() with
                | "diagram" -> [| ".dashdiagram" |]
                | "presentation" | "chrome" -> [| ".dashpresentation" |]
                | "transform" -> [| ".dashtransform" |]
                | "tooltip" -> [| ".dashtooltip" |]
                | _ -> Array.empty
            let mutable resolved = path
            for ext in extensions do
                let withExt =
                    if path.EndsWith(ext, StringComparison.OrdinalIgnoreCase) then path
                    else path + ext
                if File.Exists withExt then resolved <- withExt
            resolved

    let private loadTooltipFragment (text: string) =
        let id, definition = TooltipModuleParser.parseTooltipFileWithId text
        let map = Dictionary<string, TooltipDefinition>(StringComparer.OrdinalIgnoreCase)
        map.[id] <- definition
        { emptyFragment with Tooltips = Some(map :> IReadOnlyDictionary<_, _>) }

    let rec load (includeKind: string) (reference: string) (specDirectory: string) =
        let path = SpecFragmentPaths.resolvePath reference specDirectory
        let resolved = resolveExistingFile path includeKind
        if not (File.Exists resolved) then
            raise (FileNotFoundException($"Include {includeKind} not found: '{reference}' (resolved: {resolved}).", resolved))

        let text = File.ReadAllText resolved
        let baseDirectory = Path.GetDirectoryName resolved |> Option.ofObj |> Option.defaultValue specDirectory

        match includeKind.ToLowerInvariant() with
        | "diagram" ->
            foldDiagramStatements (DiagramModuleParser.parseDiagramModule text).Statements baseDirectory
        | "presentation" | "chrome" ->
            { emptyFragment with Presentation = Some(parsePresentationFile text (Some baseDirectory)) }
        | "transform" ->
            { emptyFragment with SeriesTransform = Some(TransformModuleParser.parseTransformFile text) }
        | "tooltip" -> loadTooltipFragment text
        | _ ->
            raise (DashSpecParseException($"Include kind must be diagram, presentation, chrome, transform, or tooltip, got '{includeKind}'."))

    and private parsePresentationFile (text: string) (baseDirectory: string option) =
        let doc = PresentationModuleParser.parsePresentationModule text
        let mutable merged: PresentationBlock option = None

        for includeRef in doc.Includes do
            if baseDirectory.IsNone || String.IsNullOrWhiteSpace baseDirectory.Value then
                raise (DashSpecParseException("Presentation include requires a base directory (parse from file path)."))
            let fragment = load includeRef.Kind includeRef.Reference baseDirectory.Value
            merged <- mergePresentation merged fragment.Presentation

        mergePresentation merged doc.Local
        |> Option.defaultWith (fun () -> raise (DashSpecParseException("@presentation module requires at least one property.")))

    and private foldDiagramStatements (statements: IReadOnlyList<DiagramFragmentStatement>) (baseDirectory: string) =
        let mutable fragment = emptyFragment
        let mutable hasDiagram = false

        for statement in statements do
            match statement with
            | IncludeStatement includeRef ->
                fragment <- merge fragment (load includeRef.Kind includeRef.Reference baseDirectory)
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

    /// Fold diagram module statements into a fragment (module !include / registration).
    let foldDiagramModule (text: string) (baseDirectory: string) : SpecIncludeFragment =
        let doc = DiagramModuleParser.parseDiagramModule text
        foldDiagramStatements doc.Statements baseDirectory

    let foldDiagramModuleWithId (text: string) (baseDirectory: string) : string * SpecIncludeFragment =
        let id, doc = DiagramModuleParser.parseDiagramModuleWithId text
        id, foldDiagramStatements doc.Statements baseDirectory

    let parsePresentationBlockFromFile (text: string) (baseDirectory: string) : PresentationBlock =
        parsePresentationFile text (Some baseDirectory)
