namespace DashSpec.Modeling.Parse.Diagram

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Presentation
open DashSpec.Modeling.Parse.Tooltip

module DiagramModuleParser =

    let readIncludeReference (reader: TokenReader) =
        let kind = reader.ReadIdent()
        let reference = reader.ReadString()
        kind, reference

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

    let private parseChartChrome (reader: TokenReader) blockName =
        let props =
            PropertyBlockParser.parse reader PropertySchemas.presentation blockName false false
        toPresentationBlock props

    let private parseSeriesTransform (reader: TokenReader) : DashSpec.Modeling.Parse.Transform.SeriesTransformBlock =
        let props =
            PropertyBlockParser.parse reader PropertySchemas.seriesTransform "series" false false
        let usePreset =
            match props.TryGetValue "use" with
            | true, value -> Some value
            | false, _ -> None
        let max =
            match props.TryGetValue "max" with
            | true, raw ->
                let mutable parsed = 0
                if Int32.TryParse(raw, &parsed) && parsed > 0 then Some parsed else None
            | false, _ -> None
        let other =
            match props.TryGetValue "other" with
            | true, value -> Some value
            | false, _ -> None
        { UsePreset = usePreset; Max = max; OtherLabel = other }

    let private tryParseSeriesTransform (reader: TokenReader) =
        if reader.TryKeyword "series" then Some(parseSeriesTransform reader)
        elif not (reader.TryKeyword "transform") then None
        elif not (reader.TryKeyword "series") then
            raise (DashSpecParseException("Expected 'series' after transform."))
        else
            Some(parseSeriesTransform reader)

    let private tryParseDiagramKindBlock (reader: TokenReader) =
        reader.SkipNewlines()
        if reader.IsEof then None
        elif reader.TryKeyword "diagram" then Some(DiagramParser.parse reader)
        else
            match reader.TryPeekIdent() with
            | Some kind when fst (DiagramKindRegistry.tryResolve kind) -> Some(DiagramParser.parse reader)
            | _ -> None

    let parseFragmentBody (reader: TokenReader) : IReadOnlyList<DiagramFragmentStatement> =
        let statements = ResizeArray<DiagramFragmentStatement>()
        let mutable hasDiagram = false

        while not reader.IsEof do
            reader.SkipNewlines()
            if reader.IsEof then ()
            elif reader.TryKeyword "include" then
                let kind, reference = readIncludeReference reader
                statements.Add(IncludeStatement { Kind = kind; Reference = reference })
                reader.SkipNewlines()
            else
                match tryParseDiagramKindBlock reader with
                | Some diagram ->
                    hasDiagram <- true
                    statements.Add(DiagramStatement diagram)
                    reader.SkipNewlines()
                | None ->
                    if reader.TryKeyword "presentation" then
                        statements.Add(PresentationStatement(parseChartChrome reader "presentation"))
                        reader.SkipNewlines()
                    elif reader.TryKeyword "chrome" then
                        statements.Add(PresentationStatement(parseChartChrome reader "chrome"))
                        reader.SkipNewlines()
                    elif
                        match tryParseSeriesTransform reader with
                        | Some transform ->
                            statements.Add(SeriesTransformStatement transform)
                            reader.SkipNewlines()
                            true
                        | None -> false
                    then
                        ()
                    elif reader.TryKeyword "tooltip" then
                        let tooltipId = reader.ReadIdent()
                        if String.IsNullOrWhiteSpace tooltipId then
                            raise (DashSpecParseException("Inline tooltip requires an id."))
                        let definition = TooltipModuleParser.parseInline reader tooltipId
                        statements.Add(TooltipStatement(tooltipId, definition))
                        reader.SkipNewlines()
                    elif reader.TryKeyword "inspect" then
                        statements.Add(InspectStatement(InspectPresentationParser.parse reader "diagram module"))
                        reader.SkipNewlines()
                    else
                        raise (reader.Unexpected())

        if not hasDiagram then
            raise (DashSpecParseException("Diagram module requires a chart kind block (e.g. heatmap … end heatmap)."))

        statements :> IReadOnlyList<_>

    let parseDiagramModule (text: string) =
        if String.IsNullOrWhiteSpace text then invalidArg "text" "Diagram text is required."
        let reader = ParserUtilities.createReader text
        reader.SkipFileDirectives()
        reader.Expect TokenKind.At
        reader.ExpectKeyword "diagram"
        let id = reader.ReadIdent()
        if String.IsNullOrWhiteSpace id then
            raise (DashSpecParseException("Diagram module requires @diagram <id>."))
        reader.SkipNewlines()
        let statements = parseFragmentBody reader
        { Id = id; Statements = statements }

    let parseDiagramModuleWithId (text: string) =
        let doc = parseDiagramModule text
        doc.Id, doc
