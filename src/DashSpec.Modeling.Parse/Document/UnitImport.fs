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
                let effective = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

                match diagram.UsePreset with
                | Some presetId ->
                    for entry in resolveChartChromeChain state path "diagram chrome" presetId do
                        effective.[entry.Key] <- entry.Value
                | None -> ()

                for entry in diagram.Properties do
                    effective.[entry.Key] <- entry.Value

                // Apply imported `module texts` labels for bound axis columns and table columns when not explicitly set.
                let bindingKeys = [| "x"; "y"; "category"; "value"; "series"; "reference"; "color"; "size"; "from"; "to" |]
                for bk in bindingKeys do
                    match effective.TryGetValue bk with
                    | true, col when not (String.IsNullOrWhiteSpace col) && not (effective.ContainsKey(bk + "_as")) ->
                        match state.TryResolveText(DashSpec.Modeling.Parse.Syntax.LanguageObjectKind.Field, col) with
                        | Some lbl -> effective.[bk + "_as"] <- lbl
                        | None -> ()
                    | _ -> ()

                match effective.TryGetValue "columns" with
                | true, colsRaw when not (String.IsNullOrWhiteSpace colsRaw) ->
                    let colLabels = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    match effective.TryGetValue "column_labels" with
                    | true, existing when not (String.IsNullOrWhiteSpace existing) ->
                        for line in existing.Split('\n', StringSplitOptions.RemoveEmptyEntries) do
                            let parts = line.Split('=', 2)
                            if parts.Length = 2 then
                                colLabels.[parts.[0].Trim()] <- parts.[1].Trim()
                    | _ -> ()

                    for rawCol in colsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries) do
                        let col = rawCol.Trim()
                        if not (String.IsNullOrWhiteSpace col) && not (colLabels.ContainsKey col) then
                            match state.TryResolveText(DashSpec.Modeling.Parse.Syntax.LanguageObjectKind.Field, col) with
                            | Some lbl -> colLabels.[col] <- lbl
                            | None -> ()

                    if colLabels.Count > 0 then
                        effective.["column_labels"] <- BlockGrammar.serializeColumnLabelMap colLabels
                | _ -> ()

                { diagram with
                    Properties = effective :> IReadOnlyDictionary<_, _>
                    UsePreset = None })

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

    let private builtinsRegistered = ref false

    let ensureBuiltinUnitsRegistered () =
        if not !builtinsRegistered then
            builtinsRegistered := true
            let descriptors : UnitPluginDescriptor list =
                [ { KindKeyword = "defaults"
                    ImportKind = Some ImportKind.Defaults
                    Role = UnitCompileRole.Declaration
                    Project = fun ctx -> box (DashSpec.Modeling.Parse.Defaults.DefaultsModuleParser.parseEnvelope ctx.Envelope) }
                  { KindKeyword = "texts"
                    ImportKind = Some ImportKind.Texts
                    Role = UnitCompileRole.Declaration
                    Project = fun ctx -> box (DashSpec.Modeling.Parse.Texts.TextsModuleParser.parseEnvelope ctx.Envelope) }
                  { KindKeyword = "runtime"
                    ImportKind = Some ImportKind.Runtimes
                    Role = UnitCompileRole.Declaration
                    Project = fun ctx -> box (DashSpec.Modeling.Parse.Runtime.RuntimeModuleParser.parseEnvelope ctx.Envelope) }
                  { KindKeyword = "catalog"
                    ImportKind = None
                    Role = UnitCompileRole.Root
                    Project = fun ctx -> box (DashSpec.Modeling.Parse.Catalog.CatalogParser.parseEnvelope ctx.Envelope) }
                  { KindKeyword = "host"
                    ImportKind = None
                    Role = UnitCompileRole.Root
                    Project = fun ctx -> box (DashSpec.Modeling.Parse.Host.HostModuleParser.parseEnvelope ctx.Envelope ctx.SpecDirectory) }
                  { KindKeyword = "layout"
                    ImportKind = Some ImportKind.Layouts
                    Role = UnitCompileRole.Declaration
                    Project = fun ctx -> box (LayoutModuleParser.parseEnvelope ctx.Envelope) }
                  { KindKeyword = "palette"
                    ImportKind = Some ImportKind.Palettes
                    Role = UnitCompileRole.Declaration
                    Project = fun ctx -> box (Palette.PaletteModuleParser.parseEnvelope ctx.Envelope) }
                  { KindKeyword = "presentation"
                    ImportKind = Some ImportKind.Presentations
                    Role = UnitCompileRole.Declaration
                    Project = fun ctx -> box (PresentationModuleParser.parseEnvelope ctx.Envelope) }
                  { KindKeyword = "tooltip"
                    ImportKind = Some ImportKind.Tooltips
                    Role = UnitCompileRole.Declaration
                    Project = fun ctx -> box (TooltipModuleParser.parseEnvelope ctx.Envelope) }
                  { KindKeyword = "transform"
                    ImportKind = Some ImportKind.Transforms
                    Role = UnitCompileRole.Declaration
                    Project = fun ctx -> box (Transform.TransformModuleParser.parseEnvelope ctx.Envelope) }
                  { KindKeyword = "types"
                    ImportKind = Some ImportKind.Types
                    Role = UnitCompileRole.Declaration
                    Project = fun ctx -> box (TypeModuleParser.parseTypesModule ctx.Text) }
                  { KindKeyword = "diagram"
                    ImportKind = Some ImportKind.Diagrams
                    Role = UnitCompileRole.Consumer
                    Project = fun ctx -> box (UnitFragmentResolver.foldDiagramModuleWithId ctx.Text) } ]
            for d in descriptors do
                UnitProjector.register d

    let private registerTypesFile (path: string) (state: ModuleImportState) =
        for def in TypeModuleParser.parseTypesModule (UnitSource.readUnit path) do
            state.RegisterRowType def

    let private registerDashflowText (path: string) (text: string) (state: ModuleImportState) =
        let catalog = TypeCatalog.ofDefinitions (state.ExportRowTypes().Values)
        let defaultProvider =
            state.Defaults.Values
            |> Seq.tryPick (fun d -> d.Provider)
            |> Option.map (function
                | DashSpec.Modeling.Parse.Syntax.ProviderBindingSyntax.Infer -> DashflowProviderBinding.Infer
                | DashSpec.Modeling.Parse.Syntax.ProviderBindingSyntax.Named id -> DashflowProviderBinding.Named(id.Trim()))
        let defaultTimeZone =
            state.Defaults.Values
            |> Seq.tryPick (fun d -> d.TimeZone)
            |> Option.map (fun tz -> tz.Trim().ToLowerInvariant())
            |> Option.defaultValue ""
        state.RegisterDashflow(DashflowModuleParser.parseModuleWithDefaults text catalog defaultProvider defaultTimeZone, path)

    let private tryUnwrapUnit (text: string) : UnitGrammar.UnitEnvelope option =
        try
            let tree = DashSpec.Modeling.Parse.Syntax.DashSpecSurfaceSyntax.parse text
            UnitGrammar.tryUnwrapAny tree.Root
        with _ ->
            None

    let private importKindOfModuleKind (kind: DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind) : ImportKind option =
        match kind with
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Defaults -> Some ImportKind.Defaults
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Texts -> Some ImportKind.Texts
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Runtime -> Some ImportKind.Runtimes
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Layout -> Some ImportKind.Layouts
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Diagram -> Some ImportKind.Diagrams
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Presentation -> Some ImportKind.Presentations
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Tooltip -> Some ImportKind.Tooltips
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Types -> Some ImportKind.Types
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Flow _ -> Some ImportKind.Flows
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Palette -> Some ImportKind.Palettes
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Transform -> Some ImportKind.Transforms
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Section -> Some ImportKind.Tabs
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Registry -> Some ImportKind.Registries
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Custom kw ->
            UnitProjector.tryResolve kw |> Option.bind (fun d -> d.ImportKind)
        | _ -> None

    let private roleOfModuleKind (kind: DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind) : UnitCompileRole =
        match kind with
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Diagram
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Flow _
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Section -> UnitCompileRole.Consumer
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Custom kw ->
            UnitProjector.tryResolve kw
            |> Option.map (fun d -> d.Role)
            |> Option.defaultValue UnitCompileRole.Consumer
        | _ -> UnitCompileRole.Declaration

    let private registerEnvelopeWithQualifier
        (path: string)
        (text: string)
        (env: UnitGrammar.UnitEnvelope)
        (moduleKind: DocumentModuleKind)
        (state: ModuleImportState)
        (qualifier: string option)
        =
        let withQualifier (id: string) (reg: string -> unit) =
            reg id
            match qualifier with
            | Some q when not (String.IsNullOrWhiteSpace q) && not (String.Equals(q, id, StringComparison.OrdinalIgnoreCase)) ->
                let qid = $"{q}.{id}"
                try reg qid with _ -> ()
            | _ -> ()

        match env.Kind with
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Defaults ->
            let doc = DashSpec.Modeling.Parse.Defaults.DefaultsModuleParser.parseEnvelope env
            state.RegisterDefaults(doc.Id, doc)
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Texts ->
            let doc = DashSpec.Modeling.Parse.Texts.TextsModuleParser.parseEnvelope env
            state.RegisterTexts doc
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Runtime ->
            let doc = DashSpec.Modeling.Parse.Runtime.RuntimeModuleParser.parseEnvelope env
            state.RegisterRuntime(doc.Id, path, doc)
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Layout ->
            assignLayoutBoard (LayoutModuleParser.parseEnvelope env) moduleKind state
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Diagram ->
            let id, fragment = UnitFragmentResolver.foldDiagramModuleWithId text
            let resolved = resolveDiagramFragmentPresets path fragment state
            withQualifier id (fun k -> state.RegisterDiagram(k, resolved))
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Presentation ->
            let doc = PresentationModuleParser.parseEnvelope env
            doc.Local
            |> Option.defaultWith (fun () -> raise (DashSpecParseException("@presentation module requires at least one property.")))
            |> fun block -> withQualifier doc.Id (fun k -> state.RegisterChartChromePreset(k, block))
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Tooltip ->
            let def = TooltipModuleParser.parseEnvelope env
            withQualifier def.Id (fun k -> state.RegisterTooltip(k, def))
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Types ->
            for def in TypeModuleParser.parseTypesModule text do
                state.RegisterRowType def
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Flow _ ->
            registerDashflowText path text state
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Palette ->
            let palette = Palette.PaletteModuleParser.parseEnvelope env
            withQualifier palette.Id (fun k -> state.RegisterPalette(k, palette))
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Transform ->
            try
                let block = Transform.TransformModuleParser.parseEnvelope env
                withQualifier env.Id (fun k -> state.RegisterSeriesTransformPreset(k, block))
            with _ ->
                let decl = DashSpec.Modeling.Parse.DataFlow.TransformDeclarationParser.parseDeclaration text
                TransformDeclarations.register decl
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Section ->
            withQualifier env.Id (fun k -> state.RegisterTabModule(k, path))
        | DashSpec.Modeling.Parse.Syntax.DashSpecModuleKind.Custom kw ->
            match UnitProjector.tryResolve kw with
            | Some d ->
                d.Project
                    { Text = text
                      Envelope = env
                      SpecDirectory = Path.GetDirectoryName path |> Option.ofObj }
                |> ignore
            | None ->
                raise (DashSpecUnitHeaderException($"'{path}': module kind '{kw}' not recognized."))
        | _ -> ()

    let private registerEnvelope
        (path: string)
        (text: string)
        (env: UnitGrammar.UnitEnvelope)
        (moduleKind: DocumentModuleKind)
        (state: ModuleImportState)
        =
        registerEnvelopeWithQualifier path text env moduleKind state None

    let registerUnitFile
        (path: string)
        (moduleKind: DocumentModuleKind)
        (state: ModuleImportState)
        (importKind: ImportKind option)
        =
        if not (UnitSource.existsUnit path) then
            raise (FileNotFoundException($"module file not found: '{path}'.", path))

        let text = UnitSource.readUnit path

        match tryUnwrapUnit text with
        | Some env ->
            let otherKind =
                match importKind, importKindOfModuleKind env.Kind with
                | Some expected, Some actual -> expected <> actual
                | _ -> false

            if not otherKind then
                registerEnvelope path text env moduleKind state
        | None ->
            match importKind with
            | Some ImportKind.Types -> registerTypesFile path state
            | Some ImportKind.Flows ->
                registerDashflowText path ("module flow __imported__\n" + text + "\nend flow") state
            | _ ->
                raise (DashSpecUnitHeaderException($"'{path}': module kind not recognized; expected 'module <kind> <id>'."))

    /// <summary>Two-phase registration: all declarations first (full symbol table),
    /// then consumers. Each unit file is unwrapped once and projected from its AST envelope.</summary>
    let registerUnitsInDependencyOrderWithQualifiers
        (paths: (string * ImportKind option * string option) list)
        (moduleKind: DocumentModuleKind)
        (state: ModuleImportState)
        =
        let prepared =
            paths
            |> List.map (fun (path, kind, qualifier) ->
                if not (UnitSource.existsUnit path) then
                    raise (FileNotFoundException($"module file not found: '{path}'.", path))
                let text = UnitSource.readUnit path
                let envOpt = tryUnwrapUnit text
                let role =
                    match envOpt with
                    | Some env -> roleOfModuleKind env.Kind
                    | None -> UnitCompileRole.Consumer
                role, path, text, envOpt, kind, qualifier)

        for phase in [ UnitCompileRole.Declaration; UnitCompileRole.Consumer ] do
            for role, path, text, envOpt, importKind, qualifier in prepared do
                if role = phase then
                    match envOpt with
                    | Some env ->
                        let otherKind =
                            match importKind, importKindOfModuleKind env.Kind with
                            | Some expected, Some actual -> expected <> actual
                            | _ -> false
                        if not otherKind then
                            registerEnvelopeWithQualifier path text env moduleKind state qualifier
                    | None ->
                        registerUnitFile path moduleKind state importKind

    let registerUnitsInDependencyOrder
        (paths: (string * ImportKind option) list)
        (moduleKind: DocumentModuleKind)
        (state: ModuleImportState)
        =
        let withNone = paths |> List.map (fun (p, k) -> p, k, None)
        registerUnitsInDependencyOrderWithQualifiers withNone moduleKind state

    do
        ensureBuiltinUnitsRegistered ()
