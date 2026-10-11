namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open System.IO
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Diagram
open DashSpec.Modeling.Parse.Include
open DashSpec.Modeling.Parse.Layout
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Presentation
open DashSpec.Modeling.Parse.Tooltip
open DashSpec.Modeling.Parse.Types
open DashSpec.Modeling.Parse.DataFlow

module IncludeExpander =

    let private assignLayoutBoard (board: LayoutBoardDefinition) (moduleKind: DocumentModuleKind) (state: ModuleIncludeState) =
        match moduleKind with
        | DocumentModuleKind.Dashboard ->
            state.AssignToolbarBoard(board, "!include layout (dashboard toolbar)")
        | DocumentModuleKind.Tab ->
            state.AssignLayoutBoard(board, "!include layout (tab board)")

    let private registerDiagramFile (path: string) (specDirectory: string) (state: ModuleIncludeState) =
        let baseDirectory =
            Path.GetDirectoryName path |> Option.ofObj |> Option.defaultValue specDirectory

        let id, fragment =
            SpecIncludeFragmentResolver.foldDiagramModuleWithId (File.ReadAllText path) baseDirectory

        state.RegisterDiagram(id, fragment)

    let private registerPresentationFile (path: string) (specDirectory: string) (state: ModuleIncludeState) =
        let baseDirectory =
            Path.GetDirectoryName path |> Option.ofObj |> Option.defaultValue specDirectory

        let text = File.ReadAllText path
        let id, _ = PresentationModuleParser.parsePresentationModuleWithId text
        let block = SpecIncludeFragmentResolver.parsePresentationBlockFromFile text baseDirectory
        state.RegisterChartChromePreset(id, block)

    let private registerTooltipFile (path: string) (state: ModuleIncludeState) =
        let id, definition = TooltipModuleParser.parseTooltipFileWithId (File.ReadAllText path)
        state.RegisterTooltip(id, definition)

    let private registerTypesFile (path: string) (state: ModuleIncludeState) =
        for def in TypeModuleParser.parseTypesModule (File.ReadAllText path) do
            state.RegisterRowType def

    let private registerDashflowFile (path: string) (state: ModuleIncludeState) =
        let catalog = TypeCatalog.ofDefinitions (state.ExportRowTypes().Values)
        let module' = DashflowResolver.parseFile path catalog
        state.RegisterDashflow module'

    let private peekDiagramModuleId (text: string) =
        let reader = ParserUtilities.createReader text
        reader.SkipNewlines()
        reader.ExpectKeyword "module"
        reader.ExpectKeyword "diagram"
        reader.ReadIdent()

    /// <summary>Unit kind of a compilation unit — content (`@`-root) first, extension as legacy fallback (single .ds file type).</summary>
    type UnitKind =
        | Layout
        | Diagram
        | Presentation
        | Tooltip
        | Types
        | Flow
        | IncludeBundle
        | Transform
        | Unsupported of string

    module UnitFileClassifier =

        let private peekRootKeyword (text: string) =
            try
                let reader = ParserUtilities.createReader text
                reader.SkipNewlines()
                if reader.TryKeyword "module" then
                    match reader.TryPeekIdent() with
                    | Some keyword ->
                        reader.ReadIdent() |> ignore
                        Some(keyword.ToLowerInvariant())
                    | None -> None
                else None
            with _ ->
                None

        let classify (path: string) (text: string) : UnitKind =
            match peekRootKeyword text with
            | Some "layout" -> Layout
            | Some "diagram" -> Diagram
            | Some "presentation" -> Presentation
            | Some "tooltip" -> Tooltip
            | Some "types" -> Types
            | Some "flow" -> Flow
            | Some _ -> Unsupported(Path.GetExtension(path))
            | None ->
                match Path.GetExtension(path).ToLowerInvariant() with
                | ".dashlayout" -> Layout
                | ".dashdiagram" -> Diagram
                | ".dashpresentation" -> Presentation
                | ".dashtooltip" -> Tooltip
                | ".dashtype" -> Types
                | ".dashflow" -> Flow
                | ".dashinclude" -> IncludeBundle
                | ".dashtransform" -> Transform
                | extension -> Unsupported extension

    let registerUnitFile
        (path: string)
        (specDirectory: string)
        (moduleKind: DocumentModuleKind)
        (state: ModuleIncludeState)
        (tolerateIncompleteIncludes: bool)
        =
        if not (File.Exists path) then
            raise (FileNotFoundException($"!include not found: '{path}'.", path))

        let text = File.ReadAllText path

        match UnitFileClassifier.classify path text with
        | Layout ->
            assignLayoutBoard (LayoutModuleParser.parseLayoutFile text) moduleKind state
        | Diagram -> registerDiagramFile path specDirectory state
        | Presentation -> registerPresentationFile path specDirectory state
        | Tooltip -> registerTooltipFile path state
        | Types -> registerTypesFile path state
        | Flow -> registerDashflowFile path state
        | IncludeBundle ->
            raise (DashSpecParseException($"Internal error: .dashinclude '{path}' must be expanded before registration."))
        | Transform ->
            raise (DashSpecParseException($"!include '{path}': register transform via .dashdiagram or card block, not module include."))
        | Unsupported extension ->
            raise (DashSpecParseException($"!include '{path}': unsupported extension '{extension}'."))

    let linkEnvelope
        (directives: IReadOnlyList<ModuleLinkDirective>)
        (specDirectory: string)
        (moduleKind: DocumentModuleKind)
        (state: ModuleIncludeState)
        (parseOptions: DashSpecParseOptions)
        (reportDiagramIds: ISet<string> option)
        (reportRowTypes: ISet<string> option)
        =
        if directives.Count = 0 then ()
        else
            let selective =
                directives
                |> Seq.choose (function
                    | ModuleLinkDirective.DiagramFrom(id, path) -> Some(id, path)
                    | _ -> None)
                |> dict

            let paths =
                IncludeMembership.collectUnitPaths directives specDirectory parseOptions.TolerateIncompleteIncludes

            let requireReferenced = parseOptions.LinkOnlyReferencedDiagramUnits

            for path in paths do
                let unitText = File.ReadAllText path

                let shouldRegister =
                    if not requireReferenced then true
                    else
                        match UnitFileClassifier.classify path unitText with
                        | Diagram ->
                            let diagramId = peekDiagramModuleId unitText

                            selective.ContainsKey diagramId
                            || (match reportDiagramIds with
                                | None -> true
                                | Some ids -> ids.Contains diagramId)
                        | _ -> true

                if shouldRegister then
                    registerUnitFile path specDirectory moduleKind state parseOptions.TolerateIncompleteIncludes

            for KeyValue(expectedId, path) in selective do
                let resolved = IncludeMembership.resolveExistingIncludePath path specDirectory

                if not (File.Exists resolved) then
                    raise (FileNotFoundException($"using diagram '{expectedId}' from '{path}' was not found.", resolved))

                let baseDirectory =
                    Path.GetDirectoryName resolved |> Option.ofObj |> Option.defaultValue specDirectory

                let actualId, _ =
                    SpecIncludeFragmentResolver.foldDiagramModuleWithId (File.ReadAllText resolved) baseDirectory

                if not (String.Equals(actualId, expectedId, StringComparison.OrdinalIgnoreCase)) then
                    raise (DashSpecParseException($"using diagram '{expectedId}' from '{path}' exports '@diagram {actualId}', id mismatch."))

    let expand
        (reference: string)
        (specDirectory: string)
        (moduleKind: DocumentModuleKind)
        (state: ModuleIncludeState)
        (tolerateIncompleteIncludes: bool)
        =
        if String.IsNullOrWhiteSpace reference then
            invalidArg "reference" "Include reference is required."

        if String.IsNullOrWhiteSpace specDirectory then
            invalidArg "specDirectory" "Spec directory is required."

        if tolerateIncompleteIncludes && IncludeMembership.isIncomplete reference then
            ()
        else
            let directives = ResizeArray([ ModuleLinkDirective.PathReference reference ])

            linkEnvelope directives specDirectory moduleKind state
                { DashSpecParseOptions.defaultOptions with
                    TolerateIncompleteIncludes = tolerateIncompleteIncludes }
                None
                None
