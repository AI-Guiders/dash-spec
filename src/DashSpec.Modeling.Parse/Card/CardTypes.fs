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
      Title: string option }

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
    | InvokeHandler of string * IReadOnlyDictionary<string, string>
    | GotoTab of string
    | FocusPhase of string
    | GotoPage of string
    | GotoCatalogEntry of string * IReadOnlyList<string> option

[<CLIMutable>]
type CardClickBehaviour = { Effects: IReadOnlyList<CardClickEffect> }

type CardBoundFilterChrome =
    | Chips = 0
    | Hidden = 1
    | ToolbarOnly = 2

[<CLIMutable>]
type CardChromeDefinition = { BoundFilters: CardBoundFilterChrome }

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

[<CLIMutable>]
type FilterDeriveDefinition =
    { TargetFilter: string
      SourceFilter: string
      GrainFilterName: string option }

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
      ClickBehaviour: CardClickBehaviour option
      ExtensionBlocks: IReadOnlyList<ExtensionBlockNode>
      LocalFiltersManualApply: bool
      Visibility: CardVisibilityRule option
      PhaseId: string option
      PageId: string option
      MatrixLimits: MatrixRenderLimitsDefinition option
      OversizeMessage: string option
      Chrome: CardChromeDefinition option
      Inspect: InspectPresentation option
      Tooltip: TooltipDefinition option }

module CardBindResolver =
    let dashboardToken = "dashboard"
