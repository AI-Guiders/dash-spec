namespace DashSpec.Modeling.Parse.Card

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.DataFlow

module CardInteriorFlowResolver =

    let private applyExplicitLink
        (builder: CardDiagramSlotBuilder.Builder)
        (cardId: string)
        (cardInputs: IReadOnlyDictionary<string, CardFlowInput>)
        (link: FlowLinkDef)
        =
        match cardInputs.TryGetValue link.FromNode with
        | true, flowInput -> CardDiagramSlotBuilder.applyFlowInput builder link.ToNode flowInput cardId
        | false, _ ->
            raise (
                DashSpecParseException(
                    $"Card '{cardId}': flow link producer '{link.FromNode}' is not a card input alias; declare 'input {link.FromNode} from node.port' first."
                )
            )

    let private autoWireMatchingInputs
        (builder: CardDiagramSlotBuilder.Builder)
        (cardId: string)
        (cardInputs: IReadOnlyDictionary<string, CardFlowInput>)
        =
        for KeyValue(alias, flowInput) in cardInputs do
            if builder.Slots.ContainsKey alias then
                let scratch = builder.Slots.[alias]
                if scratch.FlowInput.IsNone then
                    CardDiagramSlotBuilder.applyFlowInput builder alias flowInput cardId

    let applyToBuilder
        (builder: CardDiagramSlotBuilder.Builder)
        (cardId: string)
        (interior: CardInteriorFlowDefinition option)
        (cardInputs: IReadOnlyDictionary<string, CardFlowInput>)
        =
        match interior with
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
                applyExplicitLink builder cardId cardInputs link

            autoWireMatchingInputs builder cardId cardInputs
        | None -> autoWireMatchingInputs builder cardId cardInputs

