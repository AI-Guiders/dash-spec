namespace DashSpec.Modeling.CodeCenter

open System.Collections.Generic
open AIGuiders.Platform.Modeling.Core.Identity
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

    let private addTier (tiers: Map<NodeId, DashSpecConceptTier>) (edges: DashSpecConceptEdge list) (parentId: NodeId option) (node: DashSpecAstNode) =
        if not (DashSpecAst.isOutlineNode node) then
            tiers, edges
        else
            let kind = DashSpecConceptOntology.kindFromAst node
            let tier =
                { Id = DashSpecAst.id node
                  Kind = kind
                  Span = conceptSpan node
                  Title = DashSpecAst.tryTitle node
                  ProjectionRole = DashSpecConceptOntology.projectionRole kind }

            let tiers = tiers |> Map.add tier.Id tier

            let edges =
                match parentId with
                | None -> edges
                | Some parent ->
                    { ParentId = parent
                      ChildId = tier.Id
                      Kind = DashSpecConceptEdgeKind.Contains }
                    :: edges

            tiers, edges

    let build (tree: ParseTree) : DashSpecConceptGraph =
        let tiers = Map.empty
        let edges = []
        let rootId = DashSpecAst.id tree.Root

        let rec walk (parentId: NodeId option) (tiers, edges) (node: DashSpecAstNode) =
            let tiers, edges = addTier tiers edges parentId node
            let parentForChildren =
                if DashSpecAst.isOutlineNode node then Some(DashSpecAst.id node)
                else parentId

            let tiers, edges =
                DashSpecAst.members node
                |> Array.fold (fun state child ->
                    if DashSpecAst.isEndBlock child then state
                    else walk parentForChildren state child) (tiers, edges)

            tiers, edges

        let tiers, edges = walk None (tiers, edges) tree.Root

        { Tree = tree
          RootId = rootId
          Tiers = tiers
          Edges = edges }

    let buildFromText (text: string) =
        build (SyntaxTree.parse text)
