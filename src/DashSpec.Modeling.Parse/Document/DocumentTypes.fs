namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open DashSpec.Modeling.Parse.Card
open DashSpec.Modeling.Parse.Diagram
open DashSpec.Modeling.Parse.Filter
open DashSpec.Modeling.Parse.Include
open DashSpec.Modeling.Parse.Layout
open DashSpec.Modeling.Parse.Presentation
open DashSpec.Modeling.Parse.Tooltip
open DashSpec.Modeling.Parse.Transform

type SqlDialect =
    | TSql
    | Postgres
    | Generic

type ReportBodyMode =
    | DashboardRoot
    | TabStandalone
    | TabEmbedded

type DashboardShellMode =
    | DashboardBody
    | TabModuleStandalone
    | TabModuleEmbedded

type DocumentModuleKind =
    | Dashboard
    | Tab

[<CLIMutable>]
type TabDefinition =
    { Id: string
      Label: string option
      CardIds: IReadOnlyList<string>
      DashspecPath: string option
      LayoutBoard: LayoutBoardDefinition option }

[<CLIMutable>]
type ReportPageDefinition =
    { Id: string
      Title: string option
      LayoutBoard: LayoutBoardDefinition option
      TabId: string option
      ToolbarBoard: LayoutBoardDefinition option
      UsageDateDerive: FilterDeriveDefinition option
      FilterDefaults: IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> option
      DisplayBindings: IReadOnlyDictionary<string, string> option }

[<CLIMutable>]
type ModuleDiagramDefinition =
    { Diagram: DiagramDefinition
      Presentation: PresentationBlock option
      SeriesTransform: SeriesTransformBlock option
      Inspect: InspectPresentation option
      Tooltip: TooltipDefinition option }

[<CLIMutable>]
type ReportFormatDefaults =
    { TimeFormat: string option
      DateFormat: string option
      DateTimeFormat: string option }

[<RequireQualifiedAccess>]
module ReportFormatDefaults =
    let empty =
        { TimeFormat = None; DateFormat = None; DateTimeFormat = None }

    let merge (baseFmt: ReportFormatDefaults) (overlay: ReportFormatDefaults) =
        { TimeFormat = overlay.TimeFormat |> Option.orElse baseFmt.TimeFormat
          DateFormat = overlay.DateFormat |> Option.orElse baseFmt.DateFormat
          DateTimeFormat = overlay.DateTimeFormat |> Option.orElse baseFmt.DateTimeFormat }

[<CLIMutable>]
type DashboardDocument =
    { Id: string
      Title: string
      ConnectorId: string option
      SqlDialect: SqlDialect
      DiagramLibraryPath: string option
      PalettePath: string option
      ColorPalette: string option
      Layout: LayoutDefinition
      FiltersChrome: FiltersChromeDefinition
      Filters: IReadOnlyList<FilterDefinition>
      DashboardFilters: IReadOnlyList<string>
      Tabs: IReadOnlyList<TabDefinition>
      Cards: IReadOnlyList<CardDefinition>
      ToolbarBoard: LayoutBoardDefinition option
      ModuleExtensions: ModuleExtensionsDefinition option
      ModuleDiagrams: IReadOnlyDictionary<string, ModuleDiagramDefinition> option
      ModuleChartChromePresets: IReadOnlyDictionary<string, PresentationBlock> option
      ModuleTooltips: IReadOnlyDictionary<string, TooltipDefinition> option
      Pages: IReadOnlyList<ReportPageDefinition> option
      CommandAliases: IReadOnlyDictionary<string, string> option
      FormatDefaults: ReportFormatDefaults }

[<RequireQualifiedAccess>]
module DashboardDocument =
    let emptyModuleDiagrams =
        Dictionary<string, ModuleDiagramDefinition>(StringComparer.OrdinalIgnoreCase) :> IReadOnlyDictionary<_, _>

    let emptyModuleChartChromePresets =
        Dictionary<string, PresentationBlock>(StringComparer.OrdinalIgnoreCase) :> IReadOnlyDictionary<_, _>

    let emptyModuleTooltips =
        Dictionary<string, TooltipDefinition>(StringComparer.OrdinalIgnoreCase) :> IReadOnlyDictionary<_, _>

    let emptyCommandAliases =
        Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) :> IReadOnlyDictionary<_, _>

type DashboardDocument with
    member this.ResolvedModuleDiagrams =
        match this.ModuleDiagrams with
        | None -> DashboardDocument.emptyModuleDiagrams
        | Some diagrams -> diagrams

    member this.ResolvedChartChromePresets =
        match this.ModuleChartChromePresets with
        | None -> DashboardDocument.emptyModuleChartChromePresets
        | Some presets -> presets

    member this.ResolvedModuleTooltips =
        match this.ModuleTooltips with
        | None -> DashboardDocument.emptyModuleTooltips
        | Some tooltips -> tooltips

[<CLIMutable>]
type TabModuleContent =
    { TabId: string
      Label: string option
      Filters: IReadOnlyList<FilterDefinition>
      Cards: IReadOnlyList<CardDefinition>
      LayoutBoard: LayoutBoardDefinition option
      ModuleDiagrams: IReadOnlyDictionary<string, ModuleDiagramDefinition> option
      ModuleChartChromePresets: IReadOnlyDictionary<string, PresentationBlock> option
      ModuleTooltips: IReadOnlyDictionary<string, TooltipDefinition> option
      Pages: IReadOnlyList<ReportPageDefinition> option
      FormatDefaults: ReportFormatDefaults }

type DashboardShellContext(mode: DashboardShellMode) =
    member val Mode = mode with get, set
    member val SpecDirectory: string option = None with get, set
    member val TabModuleId: string option = None with get, set
    member val ParentFilters: IReadOnlyList<FilterDefinition> option = None with get, set
    member val Filters = ResizeArray<FilterDefinition>()
    member val ShellFilters = ResizeArray<FilterDefinition>()
    member val TabLocalFilters = ResizeArray<FilterDefinition>()
    member val DashboardFilters = ResizeArray<string>()
    member val Tabs = ResizeArray<TabDefinition>()
    member val Cards = ResizeArray<CardDefinition>()
    member val ConnectorId: string option = None with get, set
    member val ColorPalette: string option = None with get, set
    member val Layout = LayoutDefinition.Default with get, set
    member val FiltersChrome = FiltersChromeDefinition.Default with get, set
    member val LayoutBoard: LayoutBoardDefinition option = None with get, set
    member val ToolbarBoard: LayoutBoardDefinition option = None with get, set
    member val TabModuleLabel: string option = None with get, set
    member val Includes = ModuleIncludeState() with get, set
    member val ParseOptions = DashSpec.Modeling.Parse.DashSpecParseOptions.defaultOptions with get, set
    member val CurrentPhaseId: string option = None with get, set
    member val CurrentPageId: string option = None with get, set
    member val Pages = ResizeArray<ReportPageDefinition>()
    member val ModuleExtensions = { EnabledPluginIds = []; Imports = [] } with get, set
    member val CommandAliases = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    member val FormatDefaults = ReportFormatDefaults.empty with get, set
    member val FilterDefaults = FilterScopeDefaults.create ()

    member this.ResolveFilterProperty (filterName: string) (property: string) =
        FilterScopeDefaults.tryGet this.FilterDefaults filterName property

    member this.CardBindValidationFilters =
        DashboardShellContext.mergeFilterScopes this.ParentFilters (this.ShellFilters :> IReadOnlyList<_>) [| this.TabLocalFilters :> IReadOnlyList<_>; this.Filters :> IReadOnlyList<_> |]

    member this.ExportedTabLocalFilters =
        match this.Mode with
        | DashboardShellMode.TabModuleEmbedded -> this.TabLocalFilters :> IReadOnlyList<_>
        | _ -> [||]

    static member mergeFilterScopes (parent: IReadOnlyList<FilterDefinition> option) (shell: IReadOnlyList<FilterDefinition>) (additional: IReadOnlyList<FilterDefinition>[]) =
        let merged = Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase)
        match parent with
        | Some filters ->
            for filter in filters do merged.[filter.Name] <- filter
        | None -> ()
        for filter in shell do merged.[filter.Name] <- filter
        for scope in additional do
            for filter in scope do merged.[filter.Name] <- filter
        merged.Values |> Seq.toList :> IReadOnlyList<_>

    member this.AssignToolbarBoard(board: LayoutBoardDefinition, context: string) =
        if this.ToolbarBoard.IsSome then
            raise (DashSpec.Modeling.Core.DashSpecParseException($"{context} declares more than one toolbar layout board."))
        LayoutModuleScopeValidator.ensureMatchesIncludeSite board LayoutScope.Toolbar context
        this.ToolbarBoard <- Some board

    member this.AssignTabLayoutBoard(board: LayoutBoardDefinition, context: string) =
        if this.LayoutBoard.IsSome then
            raise (DashSpec.Modeling.Core.DashSpecParseException($"{context} declares more than one card layout board."))
        LayoutModuleScopeValidator.ensureMatchesIncludeSite board LayoutScope.Tab context
        this.LayoutBoard <- Some board
