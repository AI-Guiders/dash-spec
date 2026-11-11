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

    [<Fact>]
    let ``surface syntax parses type blocks into TypeFieldDeclaration and PropertyAssignment nodes`` () =
        let typesDoc =
            """
module types demo_rows
namespace Demo.Types

type ModelRow
  optional int r.org_id
  string name
end type
"""

        let tree = DashSpec.Modeling.Parse.Syntax.DashSpecSurfaceSyntax.parse typesDoc
        let typeFields =
            DashSpec.Modeling.Parse.Syntax.DashSpecAst.descendants tree.Root
            |> Seq.choose (function
                | DashSpec.Modeling.Parse.Syntax.DashSpecAstNode.TypeFieldDeclaration f -> Some f
                | _ -> None)
            |> Seq.toArray

        Assert.Equal(2, typeFields.Length)
        Assert.True(typeFields.[0].Optional)
        Assert.Equal("r.org_id", typeFields.[0].FieldName)
        Assert.False(typeFields.[1].Optional)
        Assert.Equal("name", typeFields.[1].FieldName)

        let projected = TypeModuleParser.tryProjectTypesFromAst tree.Root
        Assert.True(projected.IsSome)
        Assert.Single(projected.Value) |> ignore
        Assert.Equal("ModelRow", projected.Value.[0].Name)

        let cardDoc =
            """
module dashboard d
  report
    title = "AI"
  end report
end dashboard
"""

        let cardTree = DashSpec.Modeling.Parse.Syntax.DashSpecSurfaceSyntax.parse cardDoc
        let props =
            DashSpec.Modeling.Parse.Syntax.DashSpecAst.descendants cardTree.Root
            |> Seq.choose (function
                | DashSpec.Modeling.Parse.Syntax.DashSpecAstNode.PropertyAssignment p -> Some p
                | _ -> None)
            |> Seq.toArray

        Assert.Single(props) |> ignore
        Assert.Equal("title", props.[0].Key)
