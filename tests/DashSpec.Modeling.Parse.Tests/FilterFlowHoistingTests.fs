namespace DashSpec.Modeling.Parse.Tests

open System
open Xunit
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.DataFlow

module FilterFlowHoistingTests =

    let private node id locality columns =
        { FilterFlowHoisting.Id = id
          FilterFlowHoisting.Locality = locality
          FilterFlowHoisting.TouchedColumns = columns }

    let private db id columns = node id FilterFlowHoisting.NodeLocality.Database columns
    let private rt id columns = node id FilterFlowHoisting.NodeLocality.Runtime columns

    [<Fact>]
    let ``database nodes hoist to the prefix, order stable`` () =
        let chain = [ db "filter.product" [ "product" ]; rt "to_zone" [ "bucket_hour" ]; db "filter.host" [ "host" ] ]

        let hoisted = FilterFlowHoisting.hoist chain
        let ids = hoisted |> List.map (fun n -> n.Id)
        Assert.Equal<string list>([ "filter.product"; "filter.host"; "to_zone" ], ids)

    [<Fact>]
    let ``already prefixed chain passes through unchanged`` () =
        let chain = [ db "filter.a" [ "x" ]; rt "to_zone" [ "y" ] ]
        Assert.Equal<string list>([ "filter.a"; "to_zone" ], FilterFlowHoisting.hoist chain |> List.map (fun n -> n.Id))

    [<Fact>]
    let ``runtime node touching the filtered column blocks the lift loudly`` () =
        // to_zone derives bucket_hour; the hour filter sits after it — no commute
        let chain = [ rt "to_zone" [ "bucket_hour" ]; db "filter.hour" [ "bucket_hour" ] ]

        let ex = Assert.Throws<DashSpecParseException>(fun () -> FilterFlowHoisting.hoist chain |> ignore)
        Assert.Contains("'filter.hour'", ex.Message)
        Assert.Contains("'to_zone'", ex.Message)
        Assert.Contains("bucket_hour", ex.Message)

    [<Fact>]
    let ``runtime node on disjoint columns commutes freely`` () =
        let chain = [ rt "to_zone" [ "bucket_hour" ]; db "filter.product" [ "product" ] ]

        let hoisted = FilterFlowHoisting.hoist chain |> List.map (fun n -> n.Id)
        Assert.Equal<string list>([ "filter.product"; "to_zone" ], hoisted)

    [<Fact>]
    let ``database node without a column is a loud error (commutation unprovable)`` () =
        let chain = [ rt "to_zone" [ "x" ]; db "filter.ghost" [] ]

        let ex = Assert.Throws<DashSpecParseException>(fun () -> FilterFlowHoisting.hoist chain |> ignore)
        Assert.Contains("commutation cannot be proven", ex.Message)

    [<Fact>]
    let ``runtime plugin registry knows to_zone family`` () =
        Assert.True(FilterFlowHoisting.RuntimePluginRegistry.isRuntime "to_zone")
        Assert.True(FilterFlowHoisting.RuntimePluginRegistry.isRuntime "TIME_TZ")
        Assert.False(FilterFlowHoisting.RuntimePluginRegistry.isRuntime "reduce")
