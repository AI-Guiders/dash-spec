namespace DashSpec.Modeling.Parse.Tests

open System.IO
open Xunit
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Types
open DashSpec.Modeling.Parse.DataFlow

module DashflowModuleParserTests =

    [<Fact>]
    let ``stdlib time dashtype defines DateTime shape`` () =
        let stdlibPath =
            Path.GetFullPath(
                Path.Combine(
                    __SOURCE_DIRECTORY__,
                    "..",
                    "..",
                    "src",
                    "DashSpec.Core",
                    "stdlib",
                    "types",
                    "time.dashtype"))

        let text = File.ReadAllText stdlibPath
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
@flow infer_default

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
@flow stakeholder_peak

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
    zone = Europe/Moscow
  }
}

utilization [utilization] -> [raw] reporting_calendar

end flow stakeholder_peak
"""

        let catalog = TypeCatalog.ofDefinitions(TypeModuleParser.parseTypesModule typesText)
        let module' = DashflowModuleParser.parseModule flowText catalog

        Assert.Equal("stakeholder_peak", module'.FlowId)
        Assert.Empty(module'.Diagnostics)
        Assert.Equal(1, module'.Sources.Length)
        Assert.Equal("demo.v_daily_peak", module'.Sources.[0].From.Value)
        Assert.Equal(1, module'.Links.Length)
        Assert.Equal(Some "utilization", module'.Links.[0].FromPort)
        Assert.Equal(Some "raw", module'.Links.[0].ToPort)

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
@flow stakeholder_peak

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
    zone = Europe/Moscow
  }
}

utilization -> reporting_calendar

end flow stakeholder_peak
"""

        let catalog = TypeCatalog.ofDefinitions(TypeModuleParser.parseTypesModule typesText)
        let module' = DashflowModuleParser.parseModule flowText catalog

        Assert.Equal("stakeholder_peak", module'.FlowId)
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
@flow multi_in

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
@flow pipeline

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
@flow parent

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
@flow parent

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
