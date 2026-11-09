namespace DashSpec.Modeling.Parse.Tests

open System
open System.IO
open Xunit
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Types
open DashSpec.Modeling.Parse.DataFlow

module DashflowModuleParserTests =

    [<Fact>]
    let ``stdlib time unit defines DateTime shape`` () =
        // The stdlib ships embedded in Execution.Core; this parser test reads the unit
        // straight from the repo (it tests parsing, not resolution).
        let stdlibPath =
            Path.GetFullPath(
                Path.Combine(
                    __SOURCE_DIRECTORY__,
                    "..", "..",
                    "src", "DashSpec.Execution.Core", "stdlib",
                    "types",
                    "time.ds"))

        let text = DashSpec.Modeling.Parse.Document.UnitSource.readUnit stdlibPath
        let defs = TypeModuleParser.parseTypesModule text
        let catalog = TypeCatalog.ofDefinitions defs

        match TypeCatalog.validate catalog with
        | Result.Error errors -> Assert.Fail(String.concat "; " errors)
        | Result.Ok () -> Assert.True(TypeCatalog.tryGet catalog "DateTime" |> Option.isSome)

    [<Fact>]
    let ``source use provider infer binds manifest default`` () =
        let typesText =
            """
type UtilizationRow
  string UserSam
end type
"""

        let flowText =
            """
module flow infer_default

source utilization {
  use provider infer
  from view demo.v_daily_peak
  ports
    output stream utilization: UtilizationRow
  end ports
}

end flow infer_default
"""

        let catalog = TypeCatalog.ofDefinitions(TypeModuleParser.parseTypesModule typesText)
        let module' = DashflowModuleParser.parseModule flowText catalog
        Assert.Equal(DashflowProviderBinding.Infer, module'.Sources.[0].Provider)

    [<Fact>]
    let ``parse dashflow source transformer ports and link`` () =
        let typesText =
            """
type UtilizationRow
  string UserSam
  int UsageDay
  int ConcurrentApps
end type
"""

        let flowText =
            """
module flow demo_peak_report

source utilization {
  use provider sqlserver
  from view demo.v_daily_peak
  ports
    default output utilization
    output stream utilization: UtilizationRow
  end ports
}

transformer reporting_calendar {
  ports
    default input raw
    input stream raw: UtilizationRow
    output stream localized: UtilizationRow
  end ports
  transform use to_zone {
    zone = UTC+3
  }
}

utilization [utilization] -> [raw] reporting_calendar

end flow demo_peak_report
"""

        let catalog = TypeCatalog.ofDefinitions(TypeModuleParser.parseTypesModule typesText)
        let module' = DashflowModuleParser.parseModule flowText catalog

        Assert.Equal("demo_peak_report", module'.FlowId)
        Assert.Empty(module'.Diagnostics)
        Assert.Equal(1, module'.Sources.Length)
        Assert.Equal(SourceOrigin.Leaf(SourceFromKind.View, "demo.v_daily_peak", None), module'.Sources.[0].Origin)
        Assert.Equal(1, module'.Links.Length)
        Assert.Equal(Some "utilization", module'.Links.[0].FromPort)
        Assert.Equal(Some "raw", module'.Links.[0].ToPort)

        let step = module'.Transformers.[0].Steps |> Array.exactlyOne
        Assert.Equal("to_zone", step.PluginId)
        Assert.Equal("timeshift", step.Parameters |> Array.find (fun p -> p.Key = "zone_kind") |> fun p -> p.Value)
        Assert.Equal("UTC+3", step.Parameters |> Array.find (fun p -> p.Key = "zone") |> fun p -> p.Value)
        Assert.Equal("180", step.Parameters |> Array.find (fun p -> p.Key = "offset_minutes") |> fun p -> p.Value)

    [<Fact>]
    let ``parse flow block with arrow links and defaults`` () =
        let typesText =
            """
type UtilizationRow
  string UserSam
  int UsageDay
  int ConcurrentApps
end type
"""

        let flowText =
            """
module flow demo_peak_report

source utilization {
  use provider sqlserver
  from view demo.v_daily_peak
  ports
    default output utilization
    output stream utilization: UtilizationRow
  end ports
}

transformer reporting_calendar {
  ports
    default input raw
    input stream raw: UtilizationRow
    output stream localized: UtilizationRow
  end ports
  transform use to_zone {
    zone = UTC+3
  }
}

utilization -> reporting_calendar

end flow demo_peak_report
"""

        let catalog = TypeCatalog.ofDefinitions(TypeModuleParser.parseTypesModule typesText)
        let module' = DashflowModuleParser.parseModule flowText catalog

        Assert.Equal("demo_peak_report", module'.FlowId)
        Assert.Empty(module'.Diagnostics)
        Assert.Equal(1, module'.Links.Length)
        Assert.Equal("utilization", module'.Links.[0].FromNode)
        Assert.Equal("reporting_calendar", module'.Links.[0].ToNode)
        Assert.Equal(None, module'.Links.[0].ToPort)
        Assert.Equal(Some "raw", module'.Transformers.[0].DefaultInputPort)

        let edge =
            module'.Graph.Edges
            |> Array.find (fun e -> e.To.NodeId = "reporting_calendar")

        Assert.Equal("raw", edge.To.PortName)

    [<Fact>]
    let ``default input resolves link when transformer has multiple inputs`` () =
        let typesText =
            """
type UtilizationRow
  string UserSam
end type
"""

        let flowText =
            """
module flow multi_in

source utilization {
  use provider sqlserver
  from view demo.v_daily_peak
  ports
    output stream utilization: UtilizationRow
  end ports
}

transformer joiner {
  ports
    default input primary
    input stream primary: UtilizationRow
    input stream secondary: UtilizationRow
    output stream merged: UtilizationRow
  end ports
}

utilization -> joiner

end flow multi_in
"""

        let catalog = TypeCatalog.ofDefinitions(TypeModuleParser.parseTypesModule typesText)
        let module' = DashflowModuleParser.parseModule flowText catalog

        Assert.Empty(module'.Diagnostics)
        let edge = module'.Graph.Edges |> Array.exactlyOne
        Assert.Equal("primary", edge.To.PortName)

    [<Fact>]
    let ``parse nested flow as composite node with parent links`` () =
        let typesText =
            """
type RawRow
  string Id
end type

type RichRow
  string Id
  string Label
end type
"""

        let flowText =
            """
module flow pipeline

source ingestion {
  use provider infer
  from view demo.v_raw
  ports
    output stream raw: RawRow
  end ports
}

flow enrich
  transformer one {
    ports
      input stream in: RawRow
      output stream mid: RawRow
    end ports
  }
  transformer two {
    ports
      input stream mid: RawRow
      output stream out: RichRow
    end ports
  }
  one -> two
end flow

transformer publish {
  ports
    default input out
    input stream out: RichRow
    output stream published: RichRow
  end ports
}

ingestion [raw] -> [in] enrich
enrich [out] -> publish

end flow pipeline
"""

        let catalog = TypeCatalog.ofDefinitions(TypeModuleParser.parseTypesModule typesText)
        let module' = DashflowModuleParser.parseModule flowText catalog

        Assert.Equal(1, module'.NestedFlows.Length)
        Assert.Equal("enrich", module'.NestedFlows.[0].Id)
        Assert.Empty(module'.Diagnostics)

        match module'.Graph.Nodes.TryGetValue "enrich" with
        | false, _ -> Assert.Fail("composite node missing")
        | true, node ->
            Assert.Equal(FlowNodeKind.Composite, node.Kind)
            Assert.Equal(Some "enrich", node.InnerFlowId)

        let toEnrich =
            module'.Graph.Edges
            |> Array.find (fun e -> e.To.NodeId = "enrich")

        Assert.Equal("in", toEnrich.To.PortName)
        Assert.Equal("in", module'.NestedFlows.[0].ExternalInputs.[0].ExternalName)
        Assert.Equal("one", module'.NestedFlows.[0].ExternalInputs.[0].InnerNodeId)

    [<Fact>]
    let ``parent short port is ambiguous when two boundary nodes share inner port name`` () =
        let typesText =
            """
type Row
  string Id
end type
"""

        let flowText =
            """
module flow parent

flow joinBox
  transformer alpha {
    ports
      input stream alpha1: Row
      output stream mid: Row
    end ports
  }
  transformer beta {
    ports
      input stream alpha1: Row
      output stream mid: Row
    end ports
  }
  transformer merge {
    ports
      input stream a: Row
      input stream b: Row
      output stream out: Row
    end ports
  }
  alpha [mid] -> [a] merge
  beta [mid] -> [b] merge
end flow

source feedA {
  use provider infer
  from view demo.v_a
  ports
    output stream out: Row
  end ports
}

feedA [out] -> [alpha1] joinBox

end flow parent
"""

        let catalog = TypeCatalog.ofDefinitions(TypeModuleParser.parseTypesModule typesText)

        let ex =
            Assert.Throws<DashSpecParseException>(fun () ->
                DashflowModuleParser.parseModule flowText catalog |> ignore)

        Assert.Contains("ambiguous", ex.Message)

    [<Fact>]
    let ``parent qualified port targets one boundary when inner names collide`` () =
        let typesText =
            """
type Row
  string Id
end type
"""

        let flowText =
            """
module flow parent

flow joinBox
  transformer alpha {
    ports
      input stream alpha1: Row
      output stream mid: Row
    end ports
  }
  transformer beta {
    ports
      input stream alpha1: Row
      output stream mid: Row
    end ports
  }
  transformer merge {
    ports
      input stream a: Row
      input stream b: Row
      output stream out: Row
    end ports
  }
  alpha [mid] -> [a] merge
  beta [mid] -> [b] merge
end flow

source feedA {
  use provider infer
  from view demo.v_a
  ports
    output stream out: Row
  end ports
}

feedA [out] -> [alpha.alpha1] joinBox

end flow parent
"""

        let catalog = TypeCatalog.ofDefinitions(TypeModuleParser.parseTypesModule typesText)
        let module' = DashflowModuleParser.parseModule flowText catalog

        Assert.Empty(module'.Diagnostics)
        let edge = module'.Graph.Edges |> Array.exactlyOne
        Assert.Equal("alpha.alpha1", edge.To.PortName)

    [<Fact>]
    let ``rejects dataflow and bare flow roots`` () =
        let catalog = TypeCatalog.empty

        let dataflowRoot =
            """
dataflow x
end flow x
"""

        Assert.Throws<DashSpecParseException>(fun () ->
            DashflowModuleParser.parseModule dataflowRoot catalog |> ignore)
        |> ignore

        let bareFlow =
            """
flow x
end flow x
"""

        Assert.Throws<DashSpecParseException>(fun () ->
            DashflowModuleParser.parseModule bareFlow catalog |> ignore)
        |> ignore

    [<Fact>]
    let ``iana_to_timeshift converts IANA zone to offset_minutes`` () =
        let previous = ZoneResolveReference.Utc
        ZoneResolveReference.Utc <- System.DateTime(2026, 1, 15, 12, 0, 0, System.DateTimeKind.Utc)

        try
            let typesText =
                """
type UtilizationRow
  string UserSam
end type
"""

            let flowText =
                """
module flow iana_zone

source utilization {
  use provider infer
  from view demo.v_daily_peak
  ports
    output stream utilization: UtilizationRow
  end ports
}

transformer reporting_calendar {
  ports
    input stream raw: UtilizationRow
    output stream localized: UtilizationRow
  end ports
  transform use iana_to_timeshift {
    iana = Europe/Moscow
  }
}

utilization -> reporting_calendar

end flow iana_zone
"""

            let catalog = TypeCatalog.ofDefinitions(TypeModuleParser.parseTypesModule typesText)
            let module' = DashflowModuleParser.parseModule flowText catalog
            let step = module'.Transformers.[0].Steps |> Array.exactlyOne

            Assert.Equal("iana_to_timeshift", step.PluginId)
            Assert.Equal("Europe/Moscow", step.Parameters |> Array.find (fun p -> p.Key = "iana") |> fun p -> p.Value)
            Assert.Equal("180", step.Parameters |> Array.find (fun p -> p.Key = "offset_minutes") |> fun p -> p.Value)
        finally
            ZoneResolveReference.Utc <- previous

    [<Fact>]
    let ``to_zone rejects IANA zone literal`` () =
        let typesText =
            """
type UtilizationRow
  string UserSam
end type
"""

        let flowText =
            """
module flow bad_to_zone

source utilization {
  use provider infer
  from view demo.v_daily_peak
  ports
    output stream utilization: UtilizationRow
  end ports
}

transformer reporting_calendar {
  ports
    input stream raw: UtilizationRow
    output stream localized: UtilizationRow
  end ports
  transform use to_zone {
    zone = Europe/Moscow
  }
}

utilization -> reporting_calendar

end flow bad_to_zone
"""

        let catalog = TypeCatalog.ofDefinitions(TypeModuleParser.parseTypesModule typesText)

        Assert.Throws<DashSpecParseException>(fun () ->
            DashflowModuleParser.parseModule flowText catalog |> ignore)
        |> ignore

    [<Fact>]
    let ``iana_to_timeshift rejects unknown zone ids`` () =
        let typesText =
            """
type UtilizationRow
  string UserSam
end type
"""

        let flowText =
            """
module flow bad_zone

source utilization {
  use provider infer
  from view demo.v_daily_peak
  ports
    output stream utilization: UtilizationRow
  end ports
}

transformer reporting_calendar {
  ports
    input stream raw: UtilizationRow
    output stream localized: UtilizationRow
  end ports
  transform use iana_to_timeshift {
    zone = America/NotARealCity_XX
  }
}

utilization -> reporting_calendar

end flow bad_zone
"""

        let catalog = TypeCatalog.ofDefinitions(TypeModuleParser.parseTypesModule typesText)

        Assert.Throws<DashSpecParseException>(fun () ->
            DashflowModuleParser.parseModule flowText catalog |> ignore)
        |> ignore

    [<Fact>]
    let ``timeshift_to_iana resolves offset to representative iana`` () =
        let previous = ZoneResolveReference.Utc
        ZoneResolveReference.Utc <- System.DateTime(2026, 1, 15, 12, 0, 0, System.DateTimeKind.Utc)

        try
            let typesText =
                """
type UtilizationRow
  string UserSam
end type
"""

            let flowText =
                """
module flow reverse_zone

source utilization {
  use provider infer
  from view demo.v_daily_peak
  ports
    output stream utilization: UtilizationRow
  end ports
}

transformer zone_label {
  ports
    input stream raw: UtilizationRow
    output stream labeled: UtilizationRow
  end ports
  transform use timeshift_to_iana {
    zone = UTC+3
  }
}

utilization -> zone_label

end flow reverse_zone
"""

            let catalog = TypeCatalog.ofDefinitions(TypeModuleParser.parseTypesModule typesText)
            let module' = DashflowModuleParser.parseModule flowText catalog
            let step = module'.Transformers.[0].Steps |> Array.exactlyOne

            Assert.Equal("timeshift_to_iana", step.PluginId)
            Assert.Equal("180", step.Parameters |> Array.find (fun p -> p.Key = "offset_minutes") |> fun p -> p.Value)
            Assert.Equal("true", step.Parameters |> Array.find (fun p -> p.Key = "lossy") |> fun p -> p.Value)
            Assert.False(String.IsNullOrWhiteSpace(step.Parameters |> Array.find (fun p -> p.Key = "iana") |> fun p -> p.Value))
        finally
            ZoneResolveReference.Utc <- previous

    [<Fact>]
    let ``to_zone accepts offset_minutes only`` () =
        let typesText =
            """
type UtilizationRow
  string UserSam
end type
"""

        let flowText =
            """
module flow minutes_only

source utilization {
  use provider infer
  from view demo.v_daily_peak
  ports
    output stream utilization: UtilizationRow
  end ports
}

transformer reporting_calendar {
  ports
    input stream raw: UtilizationRow
    output stream localized: UtilizationRow
  end ports
  transform use to_zone {
    offset_minutes = 180
  }
}

utilization -> reporting_calendar

end flow minutes_only
"""

        let catalog = TypeCatalog.ofDefinitions(TypeModuleParser.parseTypesModule typesText)
        let module' = DashflowModuleParser.parseModule flowText catalog
        let step = module'.Transformers.[0].Steps |> Array.exactlyOne
        Assert.Equal("180", step.Parameters |> Array.exactlyOne |> fun p -> p.Value)

    [<Fact>]
    let ``legacy from view source parses to Leaf origin`` () =
        let typesText =
            """
type UtilizationRow
  string UserSam
end type
"""

        let flowText =
            """
module flow legacy_leaf

source utilization {
  use provider infer
  from view demo.v_daily_peak
  ports
    output stream utilization: UtilizationRow
  end ports
}

end flow legacy_leaf
"""

        let catalog = TypeCatalog.ofDefinitions(TypeModuleParser.parseTypesModule typesText)
        let module' = DashflowModuleParser.parseModule flowText catalog

        Assert.Equal(
            SourceOrigin.Leaf(SourceFromKind.View, "demo.v_daily_peak", None),
            module'.Sources.[0].Origin)
        Assert.Equal("UtilizationRow", module'.Sources.[0].OutputRowType)
        Assert.Equal("utilization", module'.Sources.[0].OutputPort)

    [<Fact>]
    let ``new-form source with annotation and View origin parses`` () =
        let typesText =
            """
type MyRow
  string UserSam
end type
"""

        let flowText =
            """
module flow new_form_leaf

source s1: MyRow = View(demo.v_a) {
  use provider infer
}

end flow new_form_leaf
"""

        let catalog = TypeCatalog.ofDefinitions(TypeModuleParser.parseTypesModule typesText)
        let module' = DashflowModuleParser.parseModule flowText catalog
        let source = module'.Sources.[0]

        Assert.Equal("s1", source.Id)
        Assert.Equal(SourceOrigin.Leaf(SourceFromKind.View, "demo.v_a", None), source.Origin)
        Assert.Equal("MyRow", source.OutputRowType)
        Assert.Equal(DashflowProviderBinding.Infer, source.Provider)

    [<Fact>]
    let ``new-form InnerJoin source parses with join condition`` () =
        let flowText =
            """
module flow new_form_join

source a: RowA = View(demo.v_a) {
  use provider infer
  time_zone = utc
}

source b: RowB = View(demo.v_b) {
  use provider infer
  time_zone = utc
}

source j: JR = InnerJoin(a, b) {
  on = user_id, usage_day
}

end flow new_form_join
"""

        let catalog = TypeCatalog.empty
        let module' = DashflowModuleParser.parseModule flowText catalog

        Assert.Equal(3, module'.Sources.Length)
        let join = module'.Sources.[2]
        Assert.Equal(SourceOrigin.InnerJoin("a", "b", { On = [ "user_id"; "usage_day" ]; LeftKey = []; RightKey = [] }), join.Origin)
        Assert.Equal("JR", join.OutputRowType)
        // provider/time_zone are inherited from the constituents
        Assert.Equal(DashflowProviderBinding.Infer, join.Provider)
        Assert.Equal("utc", join.TimeZone)

    [<Fact>]
    let ``composite with mismatched provider raises parse error`` () =
        let flowText =
            """
module flow provider_mismatch

source a: RowA = View(demo.v_a) {
  use provider postgres
}

source b: RowB = View(demo.v_b) {
  use provider sqlserver
}

source j: JR = InnerJoin(a, b) {
  on = user_id
}

end flow provider_mismatch
"""

        let catalog = TypeCatalog.empty
        Assert.Throws<DashSpecParseException>(fun () -> DashflowModuleParser.parseModule flowText catalog |> ignore)

    [<Fact>]
    let ``composite with mismatched time_zone raises parse error`` () =
        let flowText =
            """
module flow tz_mismatch

source a: RowA = View(demo.v_a) {
  use provider infer
  time_zone = utc
}

source b: RowB = View(demo.v_b) {
  use provider infer
  time_zone = local
}

source j: JR = InnerJoin(a, b) {
  on = user_id
}

end flow tz_mismatch
"""

        let catalog = TypeCatalog.empty
        Assert.Throws<DashSpecParseException>(fun () -> DashflowModuleParser.parseModule flowText catalog |> ignore)

    [<Fact>]
    let ``composite referencing a composite source raises parse error`` () =
        let flowText =
            """
module flow nested_composition

source a: RowA = View(demo.v_a) {
  use provider infer
}

source b: RowB = View(demo.v_b) {
  use provider infer
}

source j: JR = InnerJoin(a, b) {
  on = user_id
}

source k: KR = InnerJoin(j, b) {
  on = user_id
}

end flow nested_composition
"""

        let catalog = TypeCatalog.empty
        Assert.Throws<DashSpecParseException>(fun () -> DashflowModuleParser.parseModule flowText catalog |> ignore)

    [<Fact>]
    let ``new-form UnionAll source parses with empty body`` () =
        let flowText =
            """
module flow union_all

source a: RowA = View(demo.v_a) {
  use provider infer
}

source b: RowA = View(demo.v_b) {
  use provider infer
}

source all_events = UnionAll(a, b)

end flow union_all
"""

        let catalog = TypeCatalog.empty
        let module' = DashflowModuleParser.parseModule flowText catalog

        Assert.Equal(SourceOrigin.UnionAll("a", "b"), module'.Sources.[2].Origin)

    [<Fact>]
    let ``leaf source with on condition raises parse error`` () =
        let flowText =
            """
module flow leaf_condition

source a: RowA = View(demo.v_a) {
  use provider infer
  on = user_id
}

end flow leaf_condition
"""

        let catalog = TypeCatalog.empty
        Assert.Throws<DashSpecParseException>(fun () -> DashflowModuleParser.parseModule flowText catalog |> ignore)

    [<Fact>]
    let ``composite source with unknown constituent raises parse error`` () =
        let flowText =
            """
module flow unknown_constituent

source a: RowA = View(demo.v_a) {
  use provider infer
}

source j: JR = InnerJoin(a, ghost) {
  on = user_id
}

end flow unknown_constituent
"""

        let catalog = TypeCatalog.empty
        Assert.Throws<DashSpecParseException>(fun () -> DashflowModuleParser.parseModule flowText catalog |> ignore)

    [<Fact>]
    let ``source without origin raises parse error`` () =
        let flowText =
            """
module flow no_origin

source a: RowA {
  use provider infer
}

end flow no_origin
"""

        let catalog = TypeCatalog.empty
        Assert.Throws<DashSpecParseException>(fun () -> DashflowModuleParser.parseModule flowText catalog |> ignore)
