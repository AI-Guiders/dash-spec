namespace DashSpec.Modeling.CodeCenter

open System.Collections.Generic
open DashSpec.Modeling.Parse.Syntax

module DashSpecConceptGraphBuilder =

    let private regionEnd (node: DashSpecAstNode) =
        let mutable endPos = (DashSpecAst.span node).End

        for child in DashSpecAst.members node do
            match child with
            | DashSpecAstNode.EndBlock endBlock -> endPos <- endBlock.Span.End
            | _ -> ()

        endPos

    let private conceptSpan (node: DashSpecAstNode) =
        let span = DashSpecAst.span node

        if DashSpecAst.isOutlineNode node then
            TextSpan.Create span.Start (regionEnd node - span.Start)
        else
            span

    let private addConcept (nodes: Map<uint32, DashSpecConceptNode>) (edges: DashSpecConceptEdge list) (parentAstId: uint32 option) (node: DashSpecAstNode) =
        if not (DashSpecAst.isOutlineNode node) then
            nodes, edges
        else
            let kind = DashSpecConceptOntology.kindFromAst node
            let concept =
                { AstId = DashSpecAst.id node
                  Kind = kind
                  Span = conceptSpan node
                  Title = DashSpecAst.tryTitle node
                  ProjectionRole = DashSpecConceptOntology.projectionRole kind }

            let nodes = nodes |> Map.add concept.AstId concept

            let edges =
                match parentAstId with
                | None -> edges
                | Some parentId ->
                    { ParentAstId = parentId
                      ChildAstId = concept.AstId
                      Kind = DashSpecConceptEdgeKind.Contains }
                    :: edges

            nodes, edges

    let build (tree: ParseTree) : DashSpecConceptGraph =
        let nodes = Map.empty
        let edges = []
        let rootAstId = DashSpecAst.id tree.Root

        let rec walk (parentAstId: uint32 option) (nodes, edges) (node: DashSpecAstNode) =
            let nodes, edges = addConcept nodes edges parentAstId node
            let parentForChildren =
                if DashSpecAst.isOutlineNode node then Some(DashSpecAst.id node)
                else parentAstId

            let nodes, edges =
                DashSpecAst.members node
                |> Array.fold (fun state child ->
                    if DashSpecAst.isEndBlock child then state
                    else walk parentForChildren state child) (nodes, edges)

            nodes, edges

        let nodes, edges = walk None (nodes, edges) tree.Root

        { Tree = tree
          RootAstId = rootAstId
          Nodes = nodes
          Edges = edges }

    let buildFromText (text: string) =
        build (SyntaxTree.parse text)
