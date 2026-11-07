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

    /// <summary>Resolve a <c>use &lt;preset&gt;</c> chain against the registered
    /// chart chrome presets (ADR-0098 symbol table). The compiler owns resolution:
    /// preset chains fold here — in one place, at unit registration — so consumers
    /// (cards, view slots, runtime chrome) receive an effective property bag and
    /// never re-resolve names. Unknown preset or a <c>use</c> cycle is loud.</summary>
    let private resolveChartChromeChain (state: ModuleImportState) (path: string) (origin: string) (presetId: string) =
        let merged = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        let visited = HashSet<string>(StringComparer.OrdinalIgnoreCase)
        let mutable current = Some presetId

        while current.IsSome do
            let presetName = current.Value

            if not (visited.Add presetName) then
                raise (
                    DashSpecParseException($"'{path}': {origin} preset chain cycles through '{presetName}'.")
                )

            let mutable preset = Unchecked.defaultof<PresentationBlock>

            if not (state.TryGetChartChromePreset(presetName, &preset)) then
                raise (
                    DashSpecParseException(
                        $"'{path}': {origin} preset '{presetName}' is not declared — import its presentation module (module presentation {presetName})."
                    )
                )

            for entry in preset.Properties do
                merged[entry.Key] <- entry.Value

            current <- preset.UsePreset

        merged :> IReadOnlyDictionary<string, string>

    /// <summary>Compile-time resolution of a registered diagram fragment: <c>chrome use</c>
    /// on the diagram and a <c>use</c> on the fragment presentation fold into effective
    /// properties (explicit diagram/presentation props win over preset values). Two-phase
    /// registration guarantees every presentation unit is already in the symbol table.</summary>
    let private resolveDiagramFragmentPresets (path: string) (fragment: UnitFragment) (state: ModuleImportState) =
        let diagram =
            fragment.Diagram
            |> Option.map (fun diagram ->
                match diagram.UsePreset with
                | Some presetId ->
                    let effective = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

                    for entry in resolveChartChromeChain state path "diagram chrome" presetId do
                        effective[entry.Key] <- entry.Value

                    for entry in diagram.Properties do
                        effective[entry.Key] <- entry.Value

                    { diagram with
                        Properties = effective :> IReadOnlyDictionary<_, _>
                        UsePreset = None }
                | None -> diagram)

        let presentation =
            fragment.Presentation
            |> Option.map (fun block ->
                match block.UsePreset with
                | Some presetId ->
                    let effective = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

                    for entry in resolveChartChromeChain state path "presentation" presetId do
                        effective[entry.Key] <- entry.Value

                    for entry in block.Properties do
                        effective[entry.Key] <- entry.Value

                    { block with
                        Properties = effective :> IReadOnlyDictionary<_, _>
                        UsePreset = None }
                | None -> block)

        { fragment with Diagram = diagram; Presentation = presentation }

    let private registerDiagramFile (path: string) (state: ModuleImportState) =
        let id, fragment = UnitFragmentResolver.foldDiagramModuleWithId (UnitSource.readUnit path)
        state.RegisterDiagram(id, resolveDiagramFragmentPresets path fragment state)

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

    /// <summary>Compilation unit kind, taken from the <c>module &lt;kind&gt; &lt;id&gt;</c> header (ADR-0101).</summary>
    type UnitKind =
        | Layout
        | Diagram
        | Presentation
        | Tooltip
        | Types
        | Flow
        | Palette
        | Tab
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

        let private unitKindOfImportKind (kind: ImportKind) =
            match kind with
            | ImportKind.Layouts -> Layout
            | ImportKind.Diagrams -> Diagram
            | ImportKind.Presentations -> Presentation
            | ImportKind.Tooltips -> Tooltip
            | ImportKind.Types -> Types
            | ImportKind.Flows -> Flow
            | ImportKind.Palettes -> Palette
            | ImportKind.Tabs -> Tab

        /// Unit kind from the `module <kind> <id>` keyword, resolved through the import-kind registry.
        let classify (path: string) (text: string) : UnitKind =
            match peekRootKeyword text with
            | Some keyword ->
                match ImportKindRegistry.tryFindByModuleKeyword keyword with
                | Some kind -> unitKindOfImportKind kind
                | None -> Unsupported(Path.GetExtension path)
            | None -> Unsupported(Path.GetExtension path)

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
        | Unsupported _ -> None

    let registerUnitFile
        (path: string)
        (moduleKind: DocumentModuleKind)
        (state: ModuleImportState)
        (importKind: ImportKind option)
        =
        if not (UnitSource.existsUnit path) then
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
            | Palette ->
                let palette = Palette.PaletteModuleParser.parsePaletteFile text
                state.RegisterPalette(palette.Id, palette)
            | Tab -> state.RegisterTabModule(unitIdOf "section" path text, path)
            | Unsupported _ ->
                // Mixed module (module <name> without kind) — dispatch by import kind
                match importKind with
                | Some ImportKind.Types -> registerTypesFile path state
                | Some ImportKind.Flows ->
                    // Wrap content in flow syntax for parser compatibility
                    registerDashflowText path ("module flow __imported__\n" + text + "\nend flow") state
                | _ ->
                    raise (DashSpecParseException($"'{path}': module kind not recognized; expected 'module <kind> <id>'."))

    /// <summary>Unit role for dependency-ordered registration: declarations are
    /// order-free symbols (a presentation/type/palette registers into the symbol
    /// table), consumers fold against the table (a diagram resolves
    /// <c>chrome use</c>, a flow resolves row types). The compiler owns the order —
    /// directive order in the author's header must not decide visibility.</summary>
    type private UnitRole =
        | Declaration
        | Consumer

    module private UnitRole =
        let ofPath (path: string) =
            let text = UnitSource.readUnit path
            match UnitFileClassifier.classify path text with
            | Layout | Presentation | Tooltip | Types | Palette -> Declaration
            | Diagram | Flow | Tab -> Consumer
            | Unsupported _ -> Consumer // mixed modules dispatch by import kind at registration

    /// <summary>Two-phase registration: all declarations first (full symbol table),
    /// then consumers. A diagram whose preset is imported by a LATER directive (or
    /// only implicitly, at project level) still resolves — the author's file order
    /// stops being load-bearing.</summary>
    let registerUnitsInDependencyOrder
        (paths: (string * ImportKind option) list)
        (moduleKind: DocumentModuleKind)
        (state: ModuleImportState)
        =
        let withRoles = paths |> List.map (fun (path, kind) -> UnitRole.ofPath path, path, kind)

        for phase in [ Declaration; Consumer ] do
            for role, path, kind in withRoles do
                if role = phase then
                    registerUnitFile path moduleKind state kind
