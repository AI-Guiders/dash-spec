namespace DashSpec.Modeling.Core.Tests

open Xunit
open DashSpec.Modeling.Core

module TypeCatalogTests =

    [<Fact>]
    let ``flatten nested UDT to dotted primitive paths`` () =
        let defs =
            [ { Name = "UtcOffset"
                Fields =
                  [| { Name = "TotalMinutes"; Type = DashType.Primitive DashPrimitive.Int; Optional = false } |] }
              { Name = "Date"
                Fields =
                  [| { Name = "Year"; Type = DashType.Primitive DashPrimitive.Int; Optional = false }
                     { Name = "Month"; Type = DashType.Primitive DashPrimitive.Int; Optional = false }
                     { Name = "Day"; Type = DashType.Primitive DashPrimitive.Int; Optional = false }
                     { Name = "Offset"; Type = DashType.Named "UtcOffset"; Optional = false } |] }
              { Name = "UsageDayRow"
                Fields =
                  [| { Name = "UsageDay"; Type = DashType.Named "Date"; Optional = false }
                     { Name = "PeakConcurrentApps"; Type = DashType.Primitive DashPrimitive.Int; Optional = false } |] } ]

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
                    Fields = [| { Name = "X"; Type = DashType.Primitive DashPrimitive.Int; Optional = false } |] }
                  { Name = "B"
                    Fields = [| { Name = "Y"; Type = DashType.Primitive DashPrimitive.String; Optional = false } |] } ]

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
