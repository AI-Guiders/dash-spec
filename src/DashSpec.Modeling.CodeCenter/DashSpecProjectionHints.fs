namespace DashSpec.Modeling.CodeCenter

open System
open AIGuiders.Platform.Modeling.CodeCenter
open AIGuiders.Platform.Modeling.Core.Identity

/// Planet projection visitors over AST tiers (ADR-0067 ProjectionHints).
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

    let tiersByRole (graph: DashSpecConceptGraph) (role: DashSpecProjectionRole) =
        graph.Tiers
        |> Map.toList
        |> List.choose (fun (_, tier) ->
            if tier.ProjectionRole = role then Some tier else None)
        |> List.sortBy (fun tier -> tier.Span.Start)

    let diagramTiers graph = tiersByRole graph DashSpecProjectionRole.Diagram

    let formFieldTiers graph = tiersByRole graph DashSpecProjectionRole.FormField

    let formatPreviewLabel (tier: DashSpecConceptTier) =
        let role =
            match tier.ProjectionRole with
            | DashSpecProjectionRole.Diagram -> "diagram"
            | DashSpecProjectionRole.FormField -> "form"
            | DashSpecProjectionRole.Outline -> "outline"

        $"[{role}] {DashSpecConceptOntology.treeCaption tier}"

    let buildPreviewOutline (graph: DashSpecConceptGraph) =
        let depthById =
            let parents =
                graph.Edges
                |> List.groupBy (fun edge -> edge.ChildId)
                |> List.map (fun (child, edges) -> child, edges |> List.map (fun edge -> edge.ParentId))
                |> Map.ofList

            let rec depth nodeId =
                match Map.tryFind nodeId parents with
                | None -> 0
                | Some parentIds ->
                    parentIds |> List.map (fun parentId -> depth parentId + 1) |> List.max

            graph.Tiers |> Map.map (fun _ tier -> depth tier.Id)

        graph.Tiers
        |> Map.toList
        |> List.sortBy (fun (_, tier) -> tier.Span.Start)
        |> List.map (fun (_, tier) ->
            let indent = depthById.[tier.Id]
            $"{String(' ', indent * 2)}{formatPreviewLabel tier}")
        |> String.concat Environment.NewLine

module DashSpecProjectionBridge =

    let buildConceptGraph (text: string) = DashSpecConceptGraphBuilder.buildFromText text

    let diagramNodeIds (graph: DashSpecConceptGraph) =
        DashSpecProjectionHints.diagramTiers graph |> List.map (fun tier -> tier.Id)

    let formFieldNodeIds (graph: DashSpecConceptGraph) =
        DashSpecProjectionHints.formFieldTiers graph |> List.map (fun tier -> tier.Id)

    let containsDiagramNode (graph: DashSpecConceptGraph) (nodeId: NodeId) =
        diagramNodeIds graph |> List.exists (fun id -> id = nodeId)

    let containsFormFieldNode (graph: DashSpecConceptGraph) (nodeId: NodeId) =
        formFieldNodeIds graph |> List.exists (fun id -> id = nodeId)

    let buildPreviewOutlineFromText (text: string) =
        let graph = DashSpecConceptGraphBuilder.buildFromText text
        DashSpecProjectionHints.buildPreviewOutline graph
