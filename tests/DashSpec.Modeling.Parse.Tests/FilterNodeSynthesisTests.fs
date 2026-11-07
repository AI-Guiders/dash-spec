namespace DashSpec.Modeling.Parse.Tests

open System
open Xunit
open DashSpec.Modeling.Parse.DataFlow
open DashSpec.Modeling.Parse.Filter

module FilterNodeSynthesisTests =

    let private mkFilter kind name dependsOn defaultExpr =
        { Kind = kind
          Name = name
          DefaultExpression = defaultExpr
          ColumnReference = Some "v_activity.product"
          Label = Some name
          Widget = None
          Density = None
          MinValue = None
          MaxValue = None
          GrainFilterName = None
          SingleSelect = false
          LayoutRef = None
          BindScopeHint = None
          GrainLabels = None
          ApplyMode = "auto"
          Placement = None
          DependsOn = dependsOn }

    [<Fact>]
    let ``field filter synthesizes a param node with string param and default`` () =
        let node = FilterNodeSynthesis.synthesize FilterNodeSynthesis.FilterNodeScope.Report (mkFilter FilterKind.Field "product" (Some "platform") (Some "all"))

        Assert.Equal("filter.product", node.Id)
        Assert.Single(node.Params) |> ignore
        let param = node.Params.[0]
        Assert.Equal("product", param.Name)
        Assert.Equal("string", param.ValueType)
        Assert.Equal(Some "all", param.Default)
        Assert.Equal(Some "platform", param.DependsOn)

    [<Fact>]
    let ``date filter param is daterange, top is int — no special kinds`` () =
        let date = FilterNodeSynthesis.synthesize FilterNodeSynthesis.FilterNodeScope.Report (mkFilter FilterKind.Date "usage_date" None (Some "-7d..today"))
        let top = FilterNodeSynthesis.synthesize FilterNodeSynthesis.FilterNodeScope.Report (mkFilter FilterKind.Top "limit" None (Some "200"))

        Assert.Equal("daterange", date.Params.[0].ValueType)
        Assert.Equal("int", top.Params.[0].ValueType)

    [<Fact>]
    let ``card scope owns its instance id; report scope is shared`` () =
        let card = FilterNodeSynthesis.synthesize (FilterNodeSynthesis.FilterNodeScope.Card "peak") (mkFilter FilterKind.Field "product" None None)

        Assert.Equal("card.peak.filter.product", card.Id)
        Assert.Equal("filter.product", FilterNodeSynthesis.nodeId FilterNodeSynthesis.FilterNodeScope.Report "product")
