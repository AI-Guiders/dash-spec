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
flow stakeholder_peak

source utilization {
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
flow stakeholder_peak

source utilization {
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
flow multi_in

source utilization {
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
    let ``rejects dataflow and at-flow roots`` () =
        let catalog = TypeCatalog.empty

        let dataflowRoot =
            """
dataflow x
end flow x
"""

        Assert.Throws<DashSpecParseException>(fun () ->
            DashflowModuleParser.parseModule dataflowRoot catalog |> ignore)
        |> ignore

        let atFlow =
            """
@flow x
end flow x
"""

        Assert.Throws<DashSpecParseException>(fun () ->
            DashflowModuleParser.parseModule atFlow catalog |> ignore)
        |> ignore
