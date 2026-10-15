namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open System.IO
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Diagram
open DashSpec.Modeling.Parse.Import
open DashSpec.Modeling.Parse.Layout
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Presentation
open DashSpec.Modeling.Parse.Tooltip
open DashSpec.Modeling.Parse.Types
open DashSpec.Modeling.Parse.DataFlow

module UnitImport =

    let private assignLayoutBoard (board: LayoutBoardDefinition) (moduleKind: DocumentModuleKind) (state: ModuleImportState) =
        match moduleKind with
        | DocumentModuleKind.Dashboard ->
            state.AssignToolbarBoard(board, "layout (dashboard toolbar)")
        | DocumentModuleKind.Tab ->
            state.AssignLayoutBoard(board, "layout (tab board)")

    let private registerDiagramFile (path: string) (state: ModuleImportState) =
        let id, fragment = UnitFragmentResolver.foldDiagramModuleWithId (UnitSource.readUnit path)
        state.RegisterDiagram(id, fragment)

    let private registerPresentationFile (path: string) (state: ModuleImportState) =
        let text = UnitSource.readUnit path
        let id, _ = PresentationModuleParser.parsePresentationModuleWithId text
        state.RegisterChartChromePreset(id, UnitFragmentResolver.parsePresentationBlock text)

    let private registerTooltipFile (path: string) (state: ModuleImportState) =
        let id, definition = TooltipModuleParser.parseTooltipFileWithId (UnitSource.readUnit path)
        state.RegisterTooltip(id, definition)

    let private registerTypesFile (path: string) (state: ModuleImportState) =
        for def in TypeModuleParser.parseTypesModule (UnitSource.readUnit path) do
            state.RegisterRowType def

    let private registerDashflowText (path: string) (text: string) (state: ModuleImportState) =
        let catalog = TypeCatalog.ofDefinitions (state.ExportRowTypes().Values)
        state.RegisterDashflow(DashflowModuleParser.parseModule text catalog, path)

    let private unitIdOf (kind: string) (path: string) (text: string) =
        match UnitSource.moduleIdOf text with
        | Some id -> id
        | None -> raise (DashSpecParseException($"'{path}': {kind} module requires 'module {kind} <id>'."))

    /// <summary>Unit kind of a compilation unit — content (`@`-root) first, extension as legacy fallback (single .ds file type).</summary>
    type UnitKind =
        | Layout
        | Diagram
        | Presentation
        | Tooltip
        | Types
        | Flow
        | Palette
        | Tab
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
            | Some "palette" -> Palette
            | Some "tab" -> Tab
            | Some _ -> Unsupported(Path.GetExtension(path))
            | None ->
                Unsupported(Path.GetExtension(path))

    let private importKindOf (unitKind: UnitKind) =
        match unitKind with
        | Layout -> Some ImportKind.Layouts
        | Diagram -> Some ImportKind.Diagrams
        | Presentation -> Some ImportKind.Presentations
        | Tooltip -> Some ImportKind.Tooltips
        | Types -> Some ImportKind.Types
        | Flow -> Some ImportKind.Flows
        | Palette -> Some ImportKind.Palettes
        | Tab -> Some ImportKind.Tabs
        | Transform
        | Unsupported _ -> None

    let registerUnitFile
        (path: string)
        (moduleKind: DocumentModuleKind)
        (state: ModuleImportState)
        (importKind: ImportKind option)
        =
        if not (File.Exists path) then
            raise (FileNotFoundException($"module file not found: '{path}'.", path))

        let text = UnitSource.readUnit path
        let unitKind = UnitFileClassifier.classify path text

        let otherKind =
            match importKind, importKindOf unitKind with
            | Some expected, Some actual -> expected <> actual
            | _ -> false

        if not otherKind then
            match unitKind with
            | Layout -> assignLayoutBoard (LayoutModuleParser.parseLayoutFile text) moduleKind state
            | Diagram -> registerDiagramFile path state
            | Presentation -> registerPresentationFile path state
            | Tooltip -> registerTooltipFile path state
            | Types -> registerTypesFile path state
            | Flow -> registerDashflowText path text state
            | Palette -> state.RegisterPalette(unitIdOf "palette" path text, path)
            | Tab -> state.RegisterTabModule(unitIdOf "tab" path text, path)
            | Transform ->
                raise (DashSpecParseException($"'{path}': register transform via .dashdiagram or card block, not module import."))
            | Unsupported _ ->
                // Mixed module (module <name> without kind) — dispatch by import kind
                match importKind with
                | Some ImportKind.Types -> registerTypesFile path state
                | Some ImportKind.Flows ->
                    // Wrap content in flow syntax for parser compatibility
                    registerDashflowText path ("module flow __imported__\n" + text + "\nend flow") state
                | _ ->
                    raise (DashSpecParseException($"'{path}': module kind not recognized; expected 'module <kind> <id>'."))
