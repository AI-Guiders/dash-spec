namespace DashSpec.Modeling.Parse.Card

open System.Collections.Generic
open DashSpec.Modeling.Parse.DataSource
open DashSpec.Modeling.Parse.Diagram
open DashSpec.Modeling.Parse.Filter
open DashSpec.Modeling.Parse.Layout
open DashSpec.Modeling.Parse.Presentation
open DashSpec.Modeling.Parse.Tooltip
open DashSpec.Modeling.Parse.Transform

[<CLIMutable>]
type LegendDefinition =
    { MinLabel: string option
      MaxLabel: string option
      Title: string option
      Show: bool option }

type ShowPlacement =
    | Below = 0

type ShowFormat =
    | List = 0
    | Plain = 1
    | Kv = 2

type ShowSource =
    | Tooltip = 0
    | Cell = 1

type CardClickEffect =
    | ShowSelection of ShowPlacement * ShowFormat * ShowSource * bool * string option
    | SetFilterFromField of string * string
    | DrillTableFromCell of IReadOnlyList<string * string>
    | InvokeHandler of string * IReadOnlyDictionary<string, string>
    | GotoTab of string
    | FocusPhase of string
    | GotoPage of string
    | GotoCatalogEntry of string * IReadOnlyList<string> option

    member this.IsNavigationEffect =
        match this with
        | SetFilterFromField _
        | DrillTableFromCell _
        | GotoTab _
        | FocusPhase _
        | GotoPage _
        | GotoCatalogEntry _ -> true
        | InvokeHandler(handlerId, _) ->
            System.String.Equals(handlerId, "drill_down", System.StringComparison.OrdinalIgnoreCase)
        | ShowSelection _ -> false

    member this.RequiresInteriorSlotRefresh =
        match this with
        | DrillTableFromCell _ -> true
        | _ -> false

    member this.TryGetShowSelection =
        match this with
        | ShowSelection(placement, format, source, copyFriendly, split) ->
            Some(placement, format, source, copyFriendly, split)
        | _ -> None

[<CLIMutable>]
type CardClickBehaviour =
    { Effects: IReadOnlyList<CardClickEffect> }

    member this.HasTooltipSelection =
        this.Effects
        |> Seq.exists (function
            | ShowSelection(_, _, ShowSource.Tooltip, _, _) -> true
            | _ -> false)

type CardBoundFilterChrome =
    | Chips = 0
    | Hidden = 1
    | ToolbarOnly = 2

type CardFoldMode =
    | None = 0
    | Independent = 1

type CardsFoldPolicy =
    | None = 0
    | FocusSingle = 1

[<CLIMutable>]
type CardsChromeDefinition =
    { FoldPolicy: CardsFoldPolicy }

    static member Default = { FoldPolicy = CardsFoldPolicy.None }

[<CLIMutable>]
type CardChromeDefinition =
    { BoundFilters: CardBoundFilterChrome
      HideTitle: bool
      Fold: CardFoldMode }

type CardVisibilityMode =
    | WhenEmpty = 0
    | WhenSet = 1

[<CLIMutable>]
type CardVisibilityRule =
    { FilterName: string
      Mode: CardVisibilityMode
      Message: string option }

[<CLIMutable>]
type MatrixRenderLimitsDefinition =
    { MaxCells: int option
      MaxAxisLabels: int option }

    static member Default = { MaxCells = None; MaxAxisLabels = None }

    member this.EffectiveMaxCells =
        defaultArg this.MaxCells 2500

    member this.EffectiveMaxAxisLabels =
        defaultArg this.MaxAxisLabels 80

    member this.IsOversized(xCount: int, yCount: int) =
        xCount * yCount > this.EffectiveMaxCells
        || xCount > this.EffectiveMaxAxisLabels
        || yCount > this.EffectiveMaxAxisLabels

[<CLIMutable>]
type ExtensionBlockNode =
    { Keyword: string
      Properties: IReadOnlyDictionary<string, string>
      Nested: IReadOnlyList<ExtensionBlockNode> }

[<CLIMutable>]
type ModuleExtensionImport = { PluginId: string; AssemblyPath: string option }

[<CLIMutable>]
type ModuleExtensionsDefinition =
    { EnabledPluginIds: IReadOnlyList<string>
      Imports: IReadOnlyList<ModuleExtensionImport> }

    static member Empty = { EnabledPluginIds = []; Imports = [] }

[<CLIMutable>]
type FilterDeriveDefinition =
    { TargetFilter: string
      SourceFilter: string
      GrainFilterName: string option }

[<CLIMutable>]
type CardFlowInput =
    { Alias: string
      NodeId: string
      PortName: string }

[<CLIMutable>]
type CardInteriorFlowDefinition =
    { Sections: DashSpec.Modeling.Parse.DataFlow.FlowGraphSections }

[<CLIMutable>]
type CardDiagramSlot =
    { SlotRef: string
      Diagram: DiagramDefinition
      DataSource: DataSourceDefinition
      BoundFilters: IReadOnlyList<string>
      Legend: LegendDefinition option
      Presentation: PresentationBlock option
      SeriesTransform: SeriesTransformBlock option
      FlowInput: CardFlowInput option }

    member this.WithDataSource(dataSource: DataSourceDefinition) =
        { this with DataSource = dataSource }

[<CLIMutable>]
type CardDefinition =
    { Id: string
      Title: string
      Diagram: DiagramDefinition
      DataSource: DataSourceDefinition
      BoundFilters: IReadOnlyList<string>
      LocalFilters: IReadOnlyList<string>
      Placement: PlacementDefinition option
      TabId: string option
      LayoutRef: string option
      UseCardPreset: string option
      Legend: LegendDefinition option
      Presentation: PresentationBlock option
      SeriesTransform: SeriesTransformBlock option
      FilterHostCardId: string option
      HostedFilters: IReadOnlyList<string> option
      InteriorBoard: LayoutBoardDefinition option
      DiagramSlotRef: string option
      DiagramSlots: IReadOnlyDictionary<string, CardDiagramSlot>
      ClickBehaviour: CardClickBehaviour option
      ExtensionBlocks: IReadOnlyList<ExtensionBlockNode>
      LocalFiltersManualApply: bool
      LocalFiltersApplySplitIndex: int option
      LocalFiltersChromeBoard: LayoutBoardDefinition option
      Visibility: CardVisibilityRule option
      PhaseId: string option
      PageId: string option
      MatrixLimits: MatrixRenderLimitsDefinition option
      OversizeMessage: string option
      Chrome: CardChromeDefinition option
      Inspect: InspectPresentation option
      Tooltip: TooltipDefinition option
      FlowInput: CardFlowInput option
      CardInputs: IReadOnlyDictionary<string, CardFlowInput>
      InteriorFlow: CardInteriorFlowDefinition option }

    member this.WithTitle(title: string) =
        { this with Title = title }

    member this.WithChrome(chrome: CardChromeDefinition option) =
        { this with Chrome = chrome }

    member this.WithDiagram(diagram: DiagramDefinition) =
        { this with Diagram = diagram }

    member this.WithDataSource(dataSource: DataSourceDefinition) =
        { this with DataSource = dataSource }

    member this.WithBoundFilters(boundFilters: IReadOnlyList<string>) =
        { this with BoundFilters = boundFilters }

    member this.WithDiagramSlots(diagramSlots: IReadOnlyDictionary<string, CardDiagramSlot>) =
        { this with DiagramSlots = diagramSlots }

    member this.WithLegend(legend: LegendDefinition option) =
        { this with Legend = legend }

    member this.WithPresentation(presentation: PresentationBlock option) =
        { this with Presentation = presentation }

    member this.WithSeriesTransform(seriesTransform: SeriesTransformBlock option) =
        { this with SeriesTransform = seriesTransform }

    member this.WithFlowInput(flowInput: CardFlowInput option) =
        { this with FlowInput = flowInput }

    member this.WithInspect(inspect: InspectPresentation option) =
        { this with Inspect = inspect }

    member this.WithTooltip(tooltip: TooltipDefinition option) =
        { this with Tooltip = tooltip }


module CardBindResolver =
    let dashboardToken = "dashboard"
