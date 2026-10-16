namespace DashSpec.Modeling.Parse.Filter

open System
open System.Collections.Generic
open DashSpec.Modeling.Parse.Layout

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
      BindScopeHint: string option
      GrainLabels: IReadOnlyDictionary<string, string> option
      ApplyMode: string
      Placement: PlacementDefinition option }

    member this.IsDayWidget =
        String.Equals(this.Widget |> Option.toObj, "day", StringComparison.OrdinalIgnoreCase)

    member this.IsComboboxWidget =
        String.Equals(this.Widget |> Option.toObj, "combobox", StringComparison.OrdinalIgnoreCase)

    member this.IsSelectWidget =
        String.Equals(this.Widget |> Option.toObj, "select", StringComparison.OrdinalIgnoreCase)

    member this.IsSingleSelectField =
        this.SingleSelect || this.IsSelectWidget

    member this.IsBindScopeInline =
        String.Equals(this.BindScopeHint |> Option.toObj, "inline", StringComparison.OrdinalIgnoreCase)

    member this.IsBindScopeHidden =
        String.Equals(this.BindScopeHint |> Option.toObj, "hidden", StringComparison.OrdinalIgnoreCase)

    member this.IsBindScopeTooltip =
        match this.BindScopeHint with
        | None -> true
        | Some hint ->
            String.IsNullOrWhiteSpace hint
            || String.Equals(hint, "tooltip", StringComparison.OrdinalIgnoreCase)

[<CLIMutable>]
type FiltersChromeDefinition =
    { Layout: string
      Sticky: string
      Apply: string
      ApplyControl: string
      DebounceMs: int
      FormatGuide: string
      Cells: string }

    member this.IsApplyIconControl =
        String.Equals(this.ApplyControl, "icon", StringComparison.OrdinalIgnoreCase)

    member this.ShowsFormatGuide =
        String.Equals(this.FormatGuide, "show", StringComparison.OrdinalIgnoreCase)

    member this.IsAutoApply =
        String.Equals(this.Apply, "auto", StringComparison.OrdinalIgnoreCase)

    member this.IsBarLayout =
        String.Equals(this.Layout, "bar", StringComparison.OrdinalIgnoreCase)

    member this.IsSticky =
        not (String.Equals(this.Sticky, "none", StringComparison.OrdinalIgnoreCase))

    member this.IsStickyLine =
        String.Equals(this.Sticky, "line", StringComparison.OrdinalIgnoreCase)

    member this.IsStickyCard =
        String.Equals(this.Sticky, "card", StringComparison.OrdinalIgnoreCase)

    /// <summary>Bar toolbar: label row + control row (ADR-0022 inline cells). Off when <c>cells = inline</c>.</summary>
    member this.IsToolbarLabeledCells =
        this.IsBarLayout
        && not (String.Equals(this.Cells, "inline", StringComparison.OrdinalIgnoreCase))

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
          ApplyControl = "icon"
          DebounceMs = 400
          FormatGuide = FormatGuideHidden
          Cells = "" }
