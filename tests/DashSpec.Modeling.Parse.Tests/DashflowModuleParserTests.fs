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
    let ``parse dashflow source and transformer wires`` () =
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
  from view demo.v_daily_peak
  output utilization: rows UtilizationRow
}

transformer reporting_calendar {
  input raw from utilization.utilization
  transform use to_zone {
    zone = Europe/Moscow
  }
  output localized: rows UtilizationRow
}
"""

        let catalog = TypeCatalog.ofDefinitions(TypeModuleParser.parseTypesModule typesText)
        let module' = DashflowModuleParser.parseModule flowText catalog

        Assert.Equal("stakeholder_peak", module'.FlowId)
        Assert.Empty(module'.Diagnostics)
        Assert.Equal(1, module'.Sources.Length)
        Assert.Equal("demo.v_daily_peak", module'.Sources.[0].From.Value)

    [<Fact>]
    let ``parse dataflow block with arrow links`` () =
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
dataflow stakeholder_peak

source utilization {
  from view demo.v_daily_peak
  output utilization: rows UtilizationRow
}

transformer reporting_calendar {
  input raw
  transform use to_zone {
    zone = Europe/Moscow
  }
  output localized: rows UtilizationRow
}

utilization --> reporting_calendar raw

end dataflow stakeholder_peak
"""

        let catalog = TypeCatalog.ofDefinitions(TypeModuleParser.parseTypesModule typesText)
        let module' = DashflowModuleParser.parseModule flowText catalog

        Assert.Equal("stakeholder_peak", module'.FlowId)
        Assert.Empty(module'.Diagnostics)
        Assert.Equal(1, module'.Links.Length)
        Assert.Equal("utilization", module'.Links.[0].FromNode)
        Assert.Equal("reporting_calendar", module'.Links.[0].ToNode)
        Assert.Equal(Some "raw", module'.Links.[0].ToPort)
