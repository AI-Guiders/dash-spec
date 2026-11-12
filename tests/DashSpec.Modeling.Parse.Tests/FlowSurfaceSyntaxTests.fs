namespace DashSpec.Modeling.Parse.Tests

open Xunit
open DashSpec.Modeling.Parse.DataFlow
open DashSpec.Modeling.Parse.Syntax

/// <summary>ADR-0130 slice 2: ports-block members and flow links parse into typed
/// AST nodes (PortDeclaration / FlowLinkDeclaration) and project back into the
/// legacy grammar records (ParsedPorts / FlowLinkDef) — the tree path engages
/// only when every member is recognized.</summary>
module FlowSurfaceSyntaxTests =

    [<Fact>]
    let ``ports block parses into PortDeclaration nodes and projects ParsedPorts`` () =
        let doc =
            """
module flow inference_flow

source events {
  ports
    input stream raw: EventRow
    output stream rows: EventByAppRow
    default output rows
  end ports
}

end flow
"""

        let tree = DashSpecSurfaceSyntax.parse doc
        let ports =
            DashSpecAst.descendants tree.Root
            |> Seq.choose (function
                | DashSpecAstNode.PortDeclaration p -> Some p
                | _ -> None)
            |> Seq.toArray

        let defaultClauses =
            DashSpecAst.descendants tree.Root
            |> Seq.choose (function
                | DashSpecAstNode.ClauseDeclaration c ->
                    match c.Kind with
                    | ClauseKind.DefaultPortClause(dir, name) -> Some(dir, name)
                    | _ -> None
                | _ -> None)
            |> Seq.toArray

        Assert.Equal(2, ports.Length)
        Assert.Equal("raw", ports.[0].PortName)
        Assert.Equal("input", (match ports.[0].Role with PortDeclRole.PortInput -> "input" | _ -> "other"))
        Assert.Equal(Some PortShapeSyntax.Stream, ports.[0].PortShape)
        Assert.Equal("rows", ports.[1].PortName)
        Assert.Equal("EventByAppRow", ports.[1].ValueType |> Option.defaultValue "")
        Assert.Equal<PortDirection * string>([| (PortDirection.Output, "rows") |], defaultClauses)

        let portsBlock =
            DashSpecAst.descendants tree.Root
            |> Seq.choose (function
                | DashSpecAstNode.BlockDeclaration b ->
                    match b.Opener with
                    | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Ports -> Some b.Members
                    | _ -> None
                | _ -> None)
            |> Seq.head

        let projected = FlowPortsParser.tryProjectPortsFromAst portsBlock
        Assert.True(projected.IsSome)
        Assert.Single(projected.Value.Inputs) |> ignore
        Assert.Single(projected.Value.Outputs) |> ignore
        Assert.Equal("rows", projected.Value.DefaultOutput |> Option.defaultValue "")

    [<Fact>]
    let ``param port carries default and column binding`` () =
        let doc =
            """
module flow inference_flow

transformer filtered {
  ports
    param product: string = all bind app_name
  end ports
}

end flow
"""

        let tree = DashSpecSurfaceSyntax.parse doc
        let ports =
            DashSpecAst.descendants tree.Root
            |> Seq.choose (function
                | DashSpecAstNode.PortDeclaration p -> Some p
                | _ -> None)
            |> Seq.toArray

        Assert.Single(ports) |> ignore
        Assert.Equal("product", ports.[0].PortName)
        Assert.Equal("all", ports.[0].DefaultValue |> Option.defaultValue "")
        Assert.Equal("app_name", ports.[0].ColumnBinding |> Option.defaultValue "")

    [<Fact>]
    let ``flow links parse into FlowLinkDeclaration nodes and project FlowLinkDef`` () =
        let doc =
            """
module flow inference_flow

events [rows] -> [raw] by_app
by_app -> enriched

end flow
"""

        let tree = DashSpecSurfaceSyntax.parse doc
        let links =
            DashSpecAst.descendants tree.Root
            |> Seq.choose (function
                | DashSpecAstNode.FlowLinkDeclaration l -> Some l
                | _ -> None)
            |> Seq.toArray

        Assert.Equal(2, links.Length)
        Assert.Equal("events", links.[0].FromNode)
        Assert.Equal("rows", links.[0].FromPort |> Option.defaultValue "")
        Assert.Equal("by_app", links.[0].ToNode)
        Assert.Equal("raw", links.[0].ToPort |> Option.defaultValue "")
        Assert.Equal("by_app", links.[1].FromNode)
        Assert.True(links.[1].FromPort.IsNone)

        let flowModule =
            DashSpecAst.descendants tree.Root
            |> Seq.choose (function
                | DashSpecAstNode.ModuleDeclaration m -> Some m.Members
                | _ -> None)
            |> Seq.head

        let projected = FlowLinkParser.tryProjectLinksFromAst flowModule
        Assert.True(projected.IsSome)
        Assert.Equal(2, projected.Value.Length)

    [<Fact>]
    let ``source header Origin and body FromClause share SourceOriginSyntax and project without TokenReader`` () =
        let doc =
            """
module flow origin_ast_flow

source s_header : EventRow = View(dbo.v_events) {
  use provider infer
}

source s_legacy {
  use provider sqlserver
  from view dbo.v_legacy
  ports
    output stream rows: EventRow
  end ports
}

source s_join = InnerJoin(s_header, s_legacy) {
  on = event_id
}

transformer summarize_step {
  transform use summarize {
    by = app_name
    agg = count
  }
}

s_header -> summarize_step

end flow
"""

        let tree = DashSpecSurfaceSyntax.parse doc
        let blocks =
            DashSpecAst.descendants tree.Root
            |> Seq.choose (function
                | DashSpecAstNode.BlockDeclaration b -> Some b
                | _ -> None)
            |> Seq.toArray

        let headerBlock =
            blocks
            |> Array.find (fun b -> b.Opener = DashSpecBlockOpener.Named(DashSpecBlockKeyword.Source, "s_header"))

        Assert.Equal(Some "EventRow", headerBlock.TypeAnnotation)
        Assert.Equal(Some(SourceOriginSyntax.Leaf(SourceLeafKindSyntax.View, "dbo.v_events", None)), headerBlock.Origin)

        let joinBlock =
            blocks
            |> Array.find (fun b -> b.Opener = DashSpecBlockOpener.Named(DashSpecBlockKeyword.Source, "s_join"))

        Assert.Equal(
            Some(SourceOriginSyntax.Composite(CompositeOriginKindSyntax.InnerJoin, "s_header", "s_legacy")),
            joinBlock.Origin)

        let fromClauses =
            DashSpecAst.descendants tree.Root
            |> Seq.choose (function
                | DashSpecAstNode.ClauseDeclaration c ->
                    match c.Kind with
                    | ClauseKind.FromClause origin -> Some origin
                    | _ -> None
                | _ -> None)
            |> Seq.toArray

        Assert.Equal<SourceOriginSyntax>(
            [| SourceOriginSyntax.Leaf(SourceLeafKindSyntax.View, "dbo.v_legacy", None) |],
            fromClauses)

        let transformBlock =
            blocks
            |> Array.find (fun b -> b.Opener = DashSpecBlockOpener.Named(DashSpecBlockKeyword.Transform, "summarize"))

        let props =
            transformBlock.Members
            |> Array.choose (function
                | DashSpecAstNode.PropertyAssignment p -> Some(p.Key, p.ValueText)
                | _ -> None)

        Assert.Equal<string * string>([| ("by", "app_name"); ("agg", "count") |], props)

    [<Fact>]
    let ``Surface syntax parses first-class GridRowKind (RulerRow vs BoardRow) and GridCellTarget`` () =
        let src =
            """
module layout demo_layout
scope = section

layout grid
[] [] [] []
end grid

[ kpi_a:2 [] kpi_b:1 ]
"""

        let tree = DashSpecSurfaceSyntax.parse src

        let gridRows =
            DashSpecAst.descendants tree.Root
            |> Seq.choose (function
                | DashSpecAstNode.GridRowDeclaration r -> Some r
                | _ -> None)
            |> Seq.toArray

        Assert.Equal(2, gridRows.Length)
        Assert.Equal(GridRowKind.RulerRow 4, gridRows.[0].Kind)

        match gridRows.[1].Kind with
        | GridRowKind.BoardRow cells ->
            Assert.Equal(3, cells.Length)
            Assert.Equal(GridCellTarget.Slot(DotSyntax.Ident "kpi_a"), cells.[0].Target)
            Assert.Equal(Some 2, cells.[0].Weight)
            Assert.Equal(GridCellTarget.EmptySlot, cells.[1].Target)
            Assert.True(cells.[1].IsEmptySlot)
            Assert.Equal(1, cells.[1].EffectiveWeight)
            Assert.Equal(GridCellTarget.Slot(DotSyntax.Ident "kpi_b"), cells.[2].Target)
            Assert.Equal(Some 1, cells.[2].Weight)
        | other -> Assert.Fail($"Expected BoardRow, got {other}")

    [<Fact>]
    let ``RegistryModuleParser projects module registry unit with properties and rows`` () =
        let src =
            """
module registry custom_rules
namespace Demo.Registries

default_severity = warning

| checker      | severity |
| custom.rule1 | error    |
| custom.rule2 | info     |

end registry
"""

        let reg = DashSpec.Modeling.Parse.RegistryModuleParser.parse src
        Assert.Equal("custom_rules", reg.Id)
        Assert.Equal(Some "Demo.Registries", reg.Namespace)
        Assert.Equal("warning", reg.Properties.["default_severity"])
        Assert.Equal<string>([| "checker"; "severity" |], reg.Columns)
        Assert.Equal(2, reg.Rows.Count)
        Assert.Equal("custom.rule1", reg.Rows.[0].["checker"])
        Assert.Equal("error", reg.Rows.[0].["severity"])
