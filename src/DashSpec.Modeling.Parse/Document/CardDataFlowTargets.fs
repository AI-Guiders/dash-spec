namespace DashSpec.Modeling.Parse.Document

open System
open DashSpec.Modeling.Core

/// <summary>Resolve <c>card.&lt;cardId&gt;.&lt;slot&gt;</c> data-flow consumers (ADR-0094).</summary>
module CardDataFlowTargets =

    let tryCardSlot (toNode: string) =
        if String.IsNullOrWhiteSpace toNode then
            None
        else
            let parts = toNode.Split('.', StringSplitOptions.RemoveEmptyEntries)

            if parts.Length >= 3
               && String.Equals(parts.[0], "card", StringComparison.OrdinalIgnoreCase) then
                Some(parts.[1], parts.[2])
            else
                None

    let expectCardSlot (contextLabel: string) (toNode: string) =
        match tryCardSlot toNode with
        | Some pair -> pair
        | None ->
            raise (
                DashSpecParseException(
                    $"{contextLabel}: data-flow target '{toNode}' must be card.<cardId>.<slotRef>."
                )
            )
