namespace DashSpec.Modeling.Parse.Syntax

/// Typed block / section keyword in DashSpec surface syntax.
[<RequireQualifiedAccess>]
type DashSpecBlockKeyword =
    | Grid
    | ToolbarChrome
    | FiltersChrome
    | OnClick
    | Card
    | Tab
    | Page
    | Phase
    | Group
    | Cards
    | Views
    | Filters
    | Filter
    | Diagram
    | View
    | Layout
    | Chrome
    | Click
    | Runtime
    | Configuration
    | Wiring
    | Report
    | Extensions
    | Overrides
    | Datasource
    | Presentation
    | Data
    | Transform
    | Series
    | Inspect
    | Variables
    | Commands
    | Standalone
    | Toolbar
    | DiagramLibrary
    | Bind
    | Import
    | Include
    | Other of name: string

module DashSpecBlockKeyword =

    let tryOfName (name: string) =
        match name.ToLowerInvariant() with
        | "grid" -> Some DashSpecBlockKeyword.Grid
        | "chrome" -> Some DashSpecBlockKeyword.Chrome
        | "click" -> Some DashSpecBlockKeyword.Click
        | "card" -> Some DashSpecBlockKeyword.Card
        | "tab" -> Some DashSpecBlockKeyword.Tab
        | "page" -> Some DashSpecBlockKeyword.Page
        | "phase" -> Some DashSpecBlockKeyword.Phase
        | "group" -> Some DashSpecBlockKeyword.Group
        | "cards" -> Some DashSpecBlockKeyword.Cards
        | "views" -> Some DashSpecBlockKeyword.Views
        | "filters" -> Some DashSpecBlockKeyword.Filters
        | "filter" -> Some DashSpecBlockKeyword.Filter
        | "diagram" -> Some DashSpecBlockKeyword.Diagram
        | "view" -> Some DashSpecBlockKeyword.View
        | "layout" -> Some DashSpecBlockKeyword.Layout
        | "runtime" -> Some DashSpecBlockKeyword.Runtime
        | "configuration" -> Some DashSpecBlockKeyword.Configuration
        | "wiring" -> Some DashSpecBlockKeyword.Wiring
        | "report" -> Some DashSpecBlockKeyword.Report
        | "extensions" -> Some DashSpecBlockKeyword.Extensions
        | "overrides" -> Some DashSpecBlockKeyword.Overrides
        | "datasource" -> Some DashSpecBlockKeyword.Datasource
        | "presentation" -> Some DashSpecBlockKeyword.Presentation
        | "data" -> Some DashSpecBlockKeyword.Data
        | "transform" -> Some DashSpecBlockKeyword.Transform
        | "series" -> Some DashSpecBlockKeyword.Series
        | "inspect" -> Some DashSpecBlockKeyword.Inspect
        | "variables" -> Some DashSpecBlockKeyword.Variables
        | "commands" -> Some DashSpecBlockKeyword.Commands
        | "standalone" -> Some DashSpecBlockKeyword.Standalone
        | "toolbar" -> Some DashSpecBlockKeyword.Toolbar
        | "diagramlibrary" -> Some DashSpecBlockKeyword.DiagramLibrary
        | "bind" -> Some DashSpecBlockKeyword.Bind
        | "import" -> Some DashSpecBlockKeyword.Import
        | "include" -> Some DashSpecBlockKeyword.Include
        | "heatmap" | "gantt" | "bar" | "line" | "area" | "pie" | "donut" | "gauge" | "kpi"
        | "table" | "scatter" | "treemap" | "windrose" | "box" | "histogram" | "number" ->
            Some(DashSpecBlockKeyword.Other name)
        | _ -> None

    let isDiagramContainer (keyword: DashSpecBlockKeyword) =
        match keyword with
        | DashSpecBlockKeyword.Tab
        | DashSpecBlockKeyword.Card
        | DashSpecBlockKeyword.Page
        | DashSpecBlockKeyword.Phase
        | DashSpecBlockKeyword.Group
        | DashSpecBlockKeyword.Cards
        | DashSpecBlockKeyword.Views
        | DashSpecBlockKeyword.Layout
        | DashSpecBlockKeyword.Chrome
        | DashSpecBlockKeyword.Diagram
        | DashSpecBlockKeyword.Filters
        | DashSpecBlockKeyword.Grid -> true
        | _ -> false

    let isFormField (keyword: DashSpecBlockKeyword) =
        match keyword with
        | DashSpecBlockKeyword.Data
        | DashSpecBlockKeyword.Filter
        | DashSpecBlockKeyword.Series
        | DashSpecBlockKeyword.Datasource
        | DashSpecBlockKeyword.Transform
        | DashSpecBlockKeyword.Variables
        | DashSpecBlockKeyword.Presentation
        | DashSpecBlockKeyword.Wiring
        | DashSpecBlockKeyword.Runtime
        | DashSpecBlockKeyword.Configuration
        | DashSpecBlockKeyword.Report
        | DashSpecBlockKeyword.Bind
        | DashSpecBlockKeyword.Import -> true
        | _ -> false

    let toEndName (keyword: DashSpecBlockKeyword) =
        match keyword with
        | DashSpecBlockKeyword.Grid -> "grid"
        | DashSpecBlockKeyword.ToolbarChrome -> "chrome"
        | DashSpecBlockKeyword.FiltersChrome -> "chrome"
        | DashSpecBlockKeyword.OnClick -> "click"
        | DashSpecBlockKeyword.Card -> "card"
        | DashSpecBlockKeyword.Tab -> "tab"
        | DashSpecBlockKeyword.Page -> "page"
        | DashSpecBlockKeyword.Phase -> "phase"
        | DashSpecBlockKeyword.Group -> "group"
        | DashSpecBlockKeyword.Cards -> "cards"
        | DashSpecBlockKeyword.Views -> "views"
        | DashSpecBlockKeyword.Filters -> "filters"
        | DashSpecBlockKeyword.Filter -> "filter"
        | DashSpecBlockKeyword.Diagram -> "diagram"
        | DashSpecBlockKeyword.View -> "view"
        | DashSpecBlockKeyword.Layout -> "layout"
        | DashSpecBlockKeyword.Chrome -> "chrome"
        | DashSpecBlockKeyword.Click -> "click"
        | DashSpecBlockKeyword.Runtime -> "runtime"
        | DashSpecBlockKeyword.Configuration -> "configuration"
        | DashSpecBlockKeyword.Wiring -> "wiring"
        | DashSpecBlockKeyword.Report -> "report"
        | DashSpecBlockKeyword.Extensions -> "extensions"
        | DashSpecBlockKeyword.Overrides -> "overrides"
        | DashSpecBlockKeyword.Datasource -> "datasource"
        | DashSpecBlockKeyword.Presentation -> "presentation"
        | DashSpecBlockKeyword.Data -> "data"
        | DashSpecBlockKeyword.Transform -> "transform"
        | DashSpecBlockKeyword.Series -> "series"
        | DashSpecBlockKeyword.Inspect -> "inspect"
        | DashSpecBlockKeyword.Variables -> "variables"
        | DashSpecBlockKeyword.Commands -> "commands"
        | DashSpecBlockKeyword.Standalone -> "standalone"
        | DashSpecBlockKeyword.Toolbar -> "toolbar"
        | DashSpecBlockKeyword.DiagramLibrary -> "diagramlibrary"
        | DashSpecBlockKeyword.Bind -> "bind"
        | DashSpecBlockKeyword.Import -> "import"
        | DashSpecBlockKeyword.Include -> "include"
        | DashSpecBlockKeyword.Other name -> name
