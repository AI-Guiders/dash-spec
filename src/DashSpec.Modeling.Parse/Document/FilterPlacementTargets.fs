namespace DashSpec.Modeling.Parse.Document

open DashSpec.Modeling.Core

/// <summary>Parse-time placement targets with diagnostics (ADR-0096).</summary>
module FilterPlacementTargets =

    type PlacementTarget = FlowPlacementAnchors.PlacementAnchor

    let tryPlacementTarget = FlowPlacementAnchors.tryPlacementAnchor

    let isDataFlowCardTarget = FlowPlacementAnchors.isDataFlowCardAnchor

    let expectPlacementTarget (contextLabel: string) (toNode: string) =
        match tryPlacementTarget toNode with
        | Some target -> target
        | None ->
            raise (
                DashSpecParseException(
                    $"{contextLabel}: placement target '{toNode}' must be report, page.<pageId>, or card.<cardId>."
                )
            )
