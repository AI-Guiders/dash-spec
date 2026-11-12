namespace DashSpec.Modeling.Parse.Import

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Diagram
open DashSpec.Modeling.Parse.Layout
open DashSpec.Modeling.Parse.Presentation
open DashSpec.Modeling.Parse.Palette
open DashSpec.Modeling.Parse.Tooltip
open DashSpec.Modeling.Parse.Transform
open DashSpec.Modeling.Parse.DataFlow
open DashSpec.Modeling.Parse.Runtime

type ModuleImportState() =
    let diagrams = Dictionary<string, UnitFragment>(StringComparer.OrdinalIgnoreCase)
    let chartChromePresets = Dictionary<string, PresentationBlock>(StringComparer.OrdinalIgnoreCase)
    let seriesTransformPresets = Dictionary<string, SeriesTransformBlock>(StringComparer.OrdinalIgnoreCase)
    let tooltips = Dictionary<string, TooltipDefinition>(StringComparer.OrdinalIgnoreCase)
    let rowTypes = Dictionary<string, RowTypeDef>(StringComparer.OrdinalIgnoreCase)
    let mutable dashflowModule: DashflowModule option = None
    let mutable dashflowPath: string option = None
    let flowImportQualifiers = HashSet<string>(StringComparer.OrdinalIgnoreCase)

    let mutable layoutBoard: LayoutBoardDefinition option = None
    let mutable toolbarBoard: LayoutBoardDefinition option = None
    let aliases = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    let palettes = Dictionary<string, PaletteDocument>(StringComparer.OrdinalIgnoreCase)
    let tabModules = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    let runtimes = Dictionary<string, RuntimeDocument>(StringComparer.OrdinalIgnoreCase)
    let runtimePaths = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

    member _.RegisterRuntime(id: string, path: string, doc: RuntimeDocument) =
        runtimes.[id] <- doc
        runtimePaths.[id] <- path

    member _.Runtimes = runtimes :> IReadOnlyDictionary<string, RuntimeDocument>
    member _.RuntimePaths = runtimePaths :> IReadOnlyDictionary<string, string>

    member _.RegisterPalette(id: string, palette: PaletteDocument) =
        if palettes.ContainsKey id then
            raise (DashSpecParseException($"palette '{id}' is imported twice."))

        palettes.[id] <- palette

    member _.ResolvePalette(id: string) : PaletteDocument =
        match palettes.TryGetValue id with
        | true, palette -> palette
        | false, _ -> raise (DashSpecParseException($"palette '{id}' is not imported; add 'import palettes from <Namespace>'."))

    member _.RegisterTabModule(id: string, path: string) =
        if tabModules.ContainsKey id then
            raise (DashSpecParseException($"tab '{id}' is imported twice."))

        tabModules.[id] <- path

    member _.TryGetTabModule(id: string) =
        match tabModules.TryGetValue id with
        | true, path -> Some path
        | false, _ -> None

    member _.LayoutBoard = layoutBoard
    member _.ToolbarBoard = toolbarBoard

    member _.Aliases = aliases :> IReadOnlyDictionary<string, string>

    member _.RegisterAliases(entries: Map<string, string>) =
        for kv in entries do
            if aliases.ContainsKey kv.Key then
                raise (DashSpecParseException($"alias '{kv.Key}' is already declared in this module."))
            aliases.[kv.Key] <- kv.Value

    member _.ResolveName(name: string) =
        match aliases.TryGetValue name with
        | true, target -> target
        | false, _ -> name

    member _.TryGetDiagram(id: string, fragment: outref<UnitFragment>) =
        match diagrams.TryGetValue id with
        | true, value ->
            fragment <- value
            true
        | false, _ -> false

    member this.RegisterDiagram(id: string, fragment: UnitFragment) =
        if String.IsNullOrWhiteSpace id then invalidArg "id" "Diagram id is required."
        if diagrams.ContainsKey id then
            raise (DashSpecParseException($"Duplicate diagram id '{id}' in module imports."))
        diagrams.[id] <- fragment

    member this.RegisterTooltip(id: string, definition: TooltipDefinition) =
        if String.IsNullOrWhiteSpace id then invalidArg "id" "Tooltip id is required."
        if tooltips.ContainsKey id then
            raise (DashSpecParseException($"Duplicate tooltip id '{id}' in module imports."))
        tooltips.[id] <- definition

    member this.RegisterChartChromePreset(id: string, block: PresentationBlock) =
        if String.IsNullOrWhiteSpace id then invalidArg "id" "Preset id is required."
        if chartChromePresets.ContainsKey id then
            raise (DashSpecParseException($"Duplicate chart chrome preset '{id}' in module imports."))
        chartChromePresets.[id] <- block

    member this.RegisterSeriesTransformPreset(id: string, block: SeriesTransformBlock) =
        if String.IsNullOrWhiteSpace id then invalidArg "id" "Transform preset id is required."
        seriesTransformPresets.[id] <- block

    member this.TryGetSeriesTransformPreset(id: string) =
        match seriesTransformPresets.TryGetValue id with
        | true, value -> Some value
        | false, _ -> None

    member _.TryGetChartChromePreset(id: string, block: outref<PresentationBlock>) =
        match chartChromePresets.TryGetValue id with
        | true, value ->
            block <- value
            true
        | false, _ -> false

    member this.AssignLayoutBoard(board: LayoutBoardDefinition, context: string) =
        if layoutBoard.IsSome then
            raise (DashSpecParseException($"{context} declares more than one card layout board."))
        LayoutModuleScopeValidator.ensureMatchesImportSite board LayoutScope.Tab context
        layoutBoard <- Some board

    member this.AssignToolbarBoard(board: LayoutBoardDefinition, context: string) =
        if toolbarBoard.IsSome then
            raise (DashSpecParseException($"{context} declares more than one toolbar layout board."))
        LayoutModuleScopeValidator.ensureMatchesImportSite board LayoutScope.Toolbar context
        toolbarBoard <- Some board

    member this.RegisterRowType(definition: RowTypeDef) =
        if String.IsNullOrWhiteSpace definition.Name then invalidArg "definition" "Row type name is required."
        if rowTypes.ContainsKey definition.Name then
            raise (DashSpecParseException($"Duplicate row type '{definition.Name}' in module imports."))
        rowTypes.[definition.Name] <- definition

    member _.DashflowModule = dashflowModule
    member _.DashflowPath = dashflowPath

    member _.RegisterDashflow(module': DashflowModule, path: string) =
        if dashflowModule.IsSome then
            raise (DashSpecParseException("Module imports declare more than one flow graph."))

        dashflowModule <- Some module'
        dashflowPath <- Some path

    member _.FlowImportQualifiers = flowImportQualifiers

    member _.RegisterFlowImportQualifier(qualifier: string) =
        if not (String.IsNullOrWhiteSpace qualifier) then
            flowImportQualifiers.Add qualifier |> ignore

    member _.ExportChartChromePresets() = chartChromePresets :> IReadOnlyDictionary<_, _>
    member _.ExportTooltips() = tooltips :> IReadOnlyDictionary<_, _>
    member _.ExportRowTypes() = rowTypes :> IReadOnlyDictionary<_, _>
    member _.Diagrams = diagrams :> IReadOnlyDictionary<_, _>
