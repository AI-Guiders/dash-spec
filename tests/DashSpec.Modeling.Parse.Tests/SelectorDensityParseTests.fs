namespace DashSpec.Modeling.Parse.Tests

open System
open Xunit
open DashSpec.Modeling.Parse.Filter

module SelectorDensityParseTests =

    /// ADR-0118: the selector role's expression intent (`density = inline | collapsed`)
    /// is a first-class field on FilterDefinition; the legacy `widget =` words migrate
    /// transitionally (chips -> inline, combobox/select -> collapsed). Corpus-level
    /// parse acceptance lives in dsc validate (demo-dev-soak.ds carries density lines).

    [<Fact>]
    let ``density inline survives the record`` () =
        let filter =
            { Kind = FilterKind.Field
              Name = "app_name"
              DefaultExpression = None
              ColumnReference = Some "col"
              Label = Some "Продукты"
              Widget = None
              Density = Some "inline"
              MinValue = None
              MaxValue = None
              GrainFilterName = None
              SingleSelect = false
              LayoutRef = None
              BindScopeHint = None
              GrainLabels = None
              ApplyMode = "manual"
              Placement = None }

        Assert.Equal("inline", filter.Density |> Option.defaultValue "")
        Assert.True(filter.IsInlineDensity)

    [<Fact>]
    let ``density collapsed coexists with the legacy widget hint`` () =
        let filter =
            { Kind = FilterKind.Field
              Name = "app_name"
              DefaultExpression = None
              ColumnReference = Some "col"
              Label = None
              Widget = Some "combobox" // legacy hint rides along until spec migration
              Density = Some "collapsed"
              MinValue = None
              MaxValue = None
              GrainFilterName = None
              SingleSelect = false
              LayoutRef = None
              BindScopeHint = None
              GrainLabels = None
              ApplyMode = "manual"
              Placement = None }

        Assert.Equal("collapsed", filter.Density |> Option.defaultValue "")
        Assert.False(filter.IsInlineDensity)
        Assert.True(filter.IsComboboxWidget)
