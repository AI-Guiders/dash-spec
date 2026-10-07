namespace DashSpec.Modeling.Core

open System

/// <summary>Author-facing filter placement anchors (ADR-0096) — shared by parse lint and applicators.</summary>
module FlowPlacementAnchors =

    [<RequireQualifiedAccess>]
    type PlacementAnchor =
        | Report
        | Page of pageId: string
        | Card of cardId: string

    let tryPlacementAnchor (toNode: string) =
        if String.IsNullOrWhiteSpace toNode then
            None
        else if String.Equals(toNode, "report", StringComparison.OrdinalIgnoreCase) then
            Some PlacementAnchor.Report
        else
            let parts = toNode.Split('.', StringSplitOptions.RemoveEmptyEntries)

            match parts with
            | [| "page"; pageId |] when not (String.IsNullOrWhiteSpace pageId) -> Some(PlacementAnchor.Page pageId)
            | [| "card"; cardId |] when not (String.IsNullOrWhiteSpace cardId) -> Some(PlacementAnchor.Card cardId)
            | _ -> None

    /// <summary><c>card.&lt;id&gt;.&lt;slot&gt;</c> data-flow target (three or more segments).</summary>
    let isDataFlowCardAnchor (toNode: string) =
        if not (toNode.StartsWith("card.", StringComparison.OrdinalIgnoreCase)) then
            false
        else
            let parts = toNode.Split('.', StringSplitOptions.RemoveEmptyEntries)
            parts.Length >= 3 && String.Equals(parts.[0], "card", StringComparison.OrdinalIgnoreCase)
