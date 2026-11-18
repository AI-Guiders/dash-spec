namespace DashSpec.Modeling.Core.Tests

open Xunit
open DashSpec.Modeling.Core

module TypeCatalogTests =

    [<Fact>]
    let ``flatten nested UDT to dotted primitive paths`` () =
        let defs =
            [ { Name = "UtcOffset"
                Fields =
                  [| RowFieldDef.create "TotalMinutes" (DashType.Primitive DashPrimitive.Int) false |] }
              { Name = "Date"
                Fields =
                  [| RowFieldDef.create "Year" (DashType.Primitive DashPrimitive.Int) false
                     RowFieldDef.create "Month" (DashType.Primitive DashPrimitive.Int) false
                     RowFieldDef.create "Day" (DashType.Primitive DashPrimitive.Int) false
                     RowFieldDef.create "Offset" (DashType.Named "UtcOffset") false |] }
              { Name = "UsageDayRow"
                Fields =
                  [| RowFieldDef.create "UsageDay" (DashType.Named "Date") false
                     RowFieldDef.create "PeakConcurrentApps" (DashType.Primitive DashPrimitive.Int) false |] } ]

        let catalog = TypeCatalog.ofDefinitions defs

        match TypeCatalog.flattenRowType catalog "UsageDayRow" with
        | Result.Error message -> Assert.Fail(message)
        | Result.Ok fields ->
            Assert.Contains(fields, fun field -> field.Path = "UsageDay.Year")
            Assert.Contains(fields, fun field -> field.Path = "UsageDay.Offset.TotalMinutes")
            Assert.Contains(fields, fun field -> field.Path = "PeakConcurrentApps")

    [<Fact>]
    let ``flow graph type check rejects row mismatch`` () =
        let catalog =
            TypeCatalog.ofDefinitions
                [ { Name = "A"
                    Fields = [| RowFieldDef.create "X" (DashType.Primitive DashPrimitive.Int) false |] }
                  { Name = "B"
                    Fields = [| RowFieldDef.create "Y" (DashType.Primitive DashPrimitive.String) false |] } ]

        let graph =
            FlowGraph.ofNodes
                [ { Id = "s"
                    Kind = FlowNodeKind.Source
                    InnerFlowId = None
                    Inputs = Array.empty
                    Outputs = [| { Name = "out"; Type = DashPortType.Rows "A" } |] }
                  { Id = "t"
                    Kind = FlowNodeKind.Transformer
                    InnerFlowId = None
                    Inputs = [| { Name = "in"; Type = DashPortType.Rows "B" } |]
                    Outputs = Array.empty } ]
                [ { From = { NodeId = "s"; PortName = "out" }
                    To = { NodeId = "t"; PortName = "in" } } ]

        let diagnostics = FlowGraph.typeCheck graph catalog
        Assert.Contains(diagnostics, fun diag -> diag.Code = "DFLOW003")
