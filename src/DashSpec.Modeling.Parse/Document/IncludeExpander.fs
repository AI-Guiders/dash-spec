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
            state.AssignToolbarBoard(board, "layout (dashboard toolbar)")
        | DocumentModuleKind.Tab ->
            state.AssignLayoutBoard(board, "layout (tab board)")

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

                | ".dashtransform" -> Transform
                | extension -> Unsupported extension

    let registerUnitFile
        (path: string)
        (specDirectory: string)
        (moduleKind: DocumentModuleKind)
        (state: ModuleIncludeState)
        (importKind: ImportKind option)
        =
        if not (File.Exists path) then
            raise (FileNotFoundException($"module file not found: '{path}'.", path))

        let text = File.ReadAllText path

        match UnitFileClassifier.classify path text with
        | Layout ->
            assignLayoutBoard (LayoutModuleParser.parseLayoutFile text) moduleKind state
        | Diagram -> registerDiagramFile path specDirectory state
        | Presentation -> registerPresentationFile path specDirectory state
        | Tooltip -> registerTooltipFile path state
        | Types -> registerTypesFile path state
        | Flow -> registerDashflowFile path state
        | Transform ->
            raise (DashSpecParseException($"'{path}': register transform via .dashdiagram or card block, not module import."))
        | Unsupported extension ->
            // Mixed module (module <name> without kind) — dispatch by import kind
            match importKind with
            | Some ImportKind.Types -> registerTypesFile path state
            | Some ImportKind.Flows ->
                // Wrap content in flow syntax for parser compatibility
                let wrapped = "module flow __imported__\n" + text + "\nend flow"
                let catalog = TypeCatalog.ofDefinitions (state.ExportRowTypes().Values)
                let module' = DashflowModuleParser.parseModule wrapped catalog
                state.RegisterDashflow module'
            | _ ->
                raise (DashSpecParseException($"'{path}': unsupported extension '{extension}'."))
