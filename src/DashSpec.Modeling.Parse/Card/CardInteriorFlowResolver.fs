namespace DashSpec.Modeling.Parse.Card

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.DataFlow

module CardInteriorFlowResolver =

    let applyToBuilder
        (builder: CardDiagramSlotBuilder.Builder)
        (cardId: string)
        (interior: CardInteriorFlowDefinition option)
        (cardInputs: IReadOnlyDictionary<string, CardFlowInput>)
        =
        match interior with
        | None -> ()
        | Some flow ->
            for bind in flow.SlotBinds do
                if not (builder.Slots.ContainsKey bind.SlotRef) then
                    raise (
                        DashSpecParseException(
                            $"Card '{cardId}': flow bind references unknown diagram slot '{bind.SlotRef}'."
                        )
                    )

                let slot = builder.Slots.[bind.SlotRef]
                slot.BoundFilters.Clear()
                slot.BoundFilters.AddRange bind.FilterNames

            for link in flow.Links do
                match cardInputs.TryGetValue link.FromNode with
                | true, flowInput -> CardDiagramSlotBuilder.applyFlowInput builder link.ToNode flowInput cardId
                | false, _ ->
                    raise (
                        DashSpecParseException(
                            $"Card '{cardId}': flow link producer '{link.FromNode}' is not a card input alias; declare 'input {link.FromNode} from node.port' first."
                        )
                    )

