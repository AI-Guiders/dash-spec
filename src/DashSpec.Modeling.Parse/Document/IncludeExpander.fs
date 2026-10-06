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
        reader.Expect TokenKind.At
        reader.ExpectKeyword "diagram"
        reader.ReadIdent()

    let registerUnitFile
        (path: string)
        (specDirectory: string)
        (moduleKind: DocumentModuleKind)
        (state: ModuleIncludeState)
        (tolerateIncompleteIncludes: bool)
        =
        if not (File.Exists path) then
            raise (FileNotFoundException($"!include not found: '{path}'.", path))

        match Path.GetExtension(path).ToLowerInvariant() with
        | ".dashlayout" ->
            assignLayoutBoard (LayoutModuleParser.parseLayoutFile (File.ReadAllText path)) moduleKind state
        | ".dashdiagram" -> registerDiagramFile path specDirectory state
        | ".dashinclude" ->
            raise (DashSpecParseException($"Internal error: .dashinclude '{path}' must be expanded before registration."))
        | ".dashpresentation" -> registerPresentationFile path specDirectory state
        | ".dashtooltip" -> registerTooltipFile path state
        | ".dashtype" -> registerTypesFile path state
        | ".dashflow" -> registerDashflowFile path state
        | ".dashtransform" ->
            raise (DashSpecParseException($"!include '{path}': register transform via .dashdiagram or card block, not module include."))
        | extension ->
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
                let ext = Path.GetExtension(path).ToLowerInvariant()

                let shouldRegister =
                    if not requireReferenced then true
                    elif ext = ".dashdiagram" then
                        let diagramId = peekDiagramModuleId (File.ReadAllText path)

                        selective.ContainsKey diagramId
                        || (match reportDiagramIds with
                            | None -> true
                            | Some ids -> ids.Contains diagramId)
                    elif ext = ".dashtype" || ext = ".dashflow" || ext = ".dashlayout" || ext = ".dashpresentation" || ext = ".dashtooltip" then
                        true
                    else true

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
