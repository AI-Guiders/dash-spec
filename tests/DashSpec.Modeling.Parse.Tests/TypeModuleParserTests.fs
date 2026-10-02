namespace DashSpec.Modeling.Parse.Tests

open Xunit
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Types

module TypeModuleParserTests =

    [<Fact>]
    let ``parse type block with optional fields`` () =
        let text =
            """
type StakeholderKpiRow
  string Name
  decimal Value
end type
"""

        let defs = TypeModuleParser.parseTypesModule text
        Assert.Equal(1, defs.Length)
        Assert.Equal("StakeholderKpiRow", defs.[0].Name)
        Assert.Equal(2, defs.[0].Fields.Length)

    [<Fact>]
    let ``parse nested UDT field lines`` () =
        let text =
            """
type UtcOffset
  int TotalMinutes
end type

type UsageDayRow
  Date UsageDay
  int PeakConcurrentApps
end type

type Date
  int Year
  int Month
  int Day
  UtcOffset Offset
end type
"""

        let defs = TypeModuleParser.parseTypesModule text
        Assert.Equal(3, defs.Length)

        let usage =
            defs |> Array.find (fun def -> def.Name = "UsageDayRow")

        match usage.Fields.[0].Type with
        | DashType.Named "Date" -> ()
        | other -> Assert.Fail($"expected Date reference, got {other}")

        let catalog = DashSpec.Modeling.Core.TypeCatalog.ofDefinitions defs

        match DashSpec.Modeling.Core.TypeCatalog.flattenRowType catalog "UsageDayRow" with
        | Result.Error message -> Assert.Fail(message)
        | Result.Ok fields -> Assert.True(fields |> List.exists (fun field -> field.Path = "UsageDay.Year"))
