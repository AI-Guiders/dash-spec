namespace DashSpec.Modeling.CodeCenter

open System
open AIGuiders.Platform.Modeling.CodeCenter
open AIGuiders.Platform.Modeling.Core.Identity

/// Planet projection visitors over concept graph (ADR-0067 ProjectionHints).
module DashSpecProjectionHints =

    let availableProjections () : ProjectionDescriptor list =
        List.append
            (ProjectionDescriptor.defaultAvailable ())
            [ { Kind = DocumentProjectionKind.Diagram
                PluginId = "dashspec.diagram"
                NodeId = None
                Dialect = None }
              { Kind = DocumentProjectionKind.Form
                PluginId = "dashspec.form"
                NodeId = None
                Dialect = None }
              { Kind = DocumentProjectionKind.Preview
                PluginId = "dashspec.preview"
                NodeId = None
                Dialect = None } ]

    let nodesByRole (graph: DashSpecConceptGraph) (role: DashSpecProjectionRole) =
        graph.Nodes
        |> Map.toList
        |> List.choose (fun (_, node) ->
            if node.ProjectionRole = role then Some node else None)
        |> List.sortBy (fun node -> node.Span.Start)

    let diagramNodes graph = nodesByRole graph DashSpecProjectionRole.Diagram

    let formFieldNodes graph = nodesByRole graph DashSpecProjectionRole.FormField

    let formatPreviewLabel (node: DashSpecConceptNode) =
        let role =
            match node.ProjectionRole with
            | DashSpecProjectionRole.Diagram -> "diagram"
            | DashSpecProjectionRole.FormField -> "form"
            | DashSpecProjectionRole.Outline -> "outline"

        $"[{role}] {node.Label}"

    let buildPreviewOutline (graph: DashSpecConceptGraph) =
        let depthByAstId =
            let parents =
                graph.Edges
                |> List.groupBy (fun edge -> edge.ChildAstId)
                |> List.map (fun (child, edges) -> child, edges |> List.map (fun edge -> edge.ParentAstId))
                |> Map.ofList

            let rec depth astId =
                match Map.tryFind astId parents with
                | None -> 0
                | Some parentIds ->
                    parentIds |> List.map (fun parentId -> depth parentId + 1) |> List.max

            graph.Nodes |> Map.map (fun _ node -> depth node.AstId)

        graph.Nodes
        |> Map.toList
        |> List.sortBy (fun (_, node) -> node.Span.Start)
        |> List.map (fun (_, node) ->
            let indent = depthByAstId.[node.AstId]
            $"{String(' ', indent * 2)}{formatPreviewLabel node}")
        |> String.concat Environment.NewLine

module DashSpecProjectionBridge =

    let buildConceptGraph (text: string) = DashSpecConceptGraphBuilder.buildFromText text

    let nodeIdFromAst (astId: uint32) : NodeId = DashSpecProfileRebuild.nodeIdFromAst astId

    let diagramNodeIds (graph: DashSpecConceptGraph) =
        DashSpecProjectionHints.diagramNodes graph
        |> List.map (fun node -> nodeIdFromAst node.AstId)

    let formFieldNodeIds (graph: DashSpecConceptGraph) =
        DashSpecProjectionHints.formFieldNodes graph
        |> List.map (fun node -> nodeIdFromAst node.AstId)

    let containsDiagramNode (graph: DashSpecConceptGraph) (nodeId: NodeId) =
        diagramNodeIds graph |> List.exists (fun id -> id = nodeId)

    let containsFormFieldNode (graph: DashSpecConceptGraph) (nodeId: NodeId) =
        formFieldNodeIds graph |> List.exists (fun id -> id = nodeId)

    let buildPreviewOutlineFromText (text: string) =
        let graph = DashSpecConceptGraphBuilder.buildFromText text
        DashSpecProjectionHints.buildPreviewOutline graph
