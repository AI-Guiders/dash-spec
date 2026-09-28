namespace DashSpec.Modeling.Parse.Filter

open System.Collections.Generic

type FilterKind =
    | Date
    | Field
    | Top

[<CLIMutable>]
type FilterDefinition =
    { Kind: FilterKind
      Name: string
      DefaultExpression: string option
      ColumnReference: string option
      Label: string option
      Widget: string option
      MinValue: int option
      MaxValue: int option
      GrainFilterName: string option
      SingleSelect: bool
      LayoutRef: string option
      GrainLabels: IReadOnlyDictionary<string, string> option }

[<CLIMutable>]
type FiltersChromeDefinition =
    { Layout: string
      Sticky: string
      Apply: string
      DebounceMs: int
      FormatGuide: string }

module FiltersChromeDefinition =
    let [<Literal>] StickyNone = "none"
    let [<Literal>] StickyLine = "line"
    let [<Literal>] StickyCard = "card"
    let [<Literal>] FormatGuideHidden = "hidden"
    let [<Literal>] FormatGuideShow = "show"

    let Default =
        { Layout = "card"
          Sticky = StickyNone
          Apply = "manual"
          DebounceMs = 400
          FormatGuide = FormatGuideHidden }
