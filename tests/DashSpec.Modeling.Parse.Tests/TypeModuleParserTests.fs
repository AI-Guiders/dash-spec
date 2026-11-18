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

    [<Fact>]
    let ``parse type block supports table form, property form, and inline field format and label metadata`` () =
        let text =
            """
module types multi_form_rows
namespace Demo.Types

type TableFormRow
  | type | name       | optional | format | label     |
  | time | hour_label | true     | HH:mm  | Час суток |
  | int  | launches   | false    | N0     | Запусков  |
end type

type PropertyAndFieldRow
  hour_label = optional time as "Час суток" format = "HH:mm"
  int launches as "Запусков" format = "N0"
end type
"""

        let defs = TypeModuleParser.parseTypesModule text
        Assert.Equal(2, defs.Length)

        for def in defs do
            Assert.Equal(2, def.Fields.Length)
            let f0 = def.Fields.[0]
            let f1 = def.Fields.[1]
            Assert.Equal("hour_label", f0.Name)
            Assert.True(f0.Optional)
            Assert.Equal(DashType.Primitive DashPrimitive.Time, f0.Type)
            Assert.Equal(Some "HH:mm", f0.Format)
            Assert.Equal(Some "Час суток", f0.Label)

            Assert.Equal("launches", f1.Name)
            Assert.False(f1.Optional)
            Assert.Equal(DashType.Primitive DashPrimitive.Int, f1.Type)
            Assert.Equal(Some "N0", f1.Format)
            Assert.Equal(Some "Запусков", f1.Label)

        let schemas = RowTypeSchemaBridge.toCatalog defs
        let schema = schemas.["TableFormRow"]
        Assert.Equal("HH:mm", schema.Fields.[0].Format)
        Assert.Equal("Час суток", schema.Fields.[0].Label)
        Assert.Equal("N0", schema.Fields.[1].Format)
        Assert.Equal("Запусков", schema.Fields.[1].Label)
