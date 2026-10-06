namespace DashSpec.Modeling.Parse.Card

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.DataFlow
open DashSpec.Modeling.Parse.Filter

module CardInteriorFlowResolver =

    type private LinkClass =
        | InputAlias
        | Route
        | ModuleFlow

    let private filterNameSet (filters: IReadOnlyList<FilterDefinition>) =
        let set = HashSet<string>(StringComparer.OrdinalIgnoreCase)
        for f in filters do
            set.Add f.Name |> ignore
        set

    let private routeFilterName (link: FlowLinkDef) =
        match link.ToPort with
        | Some port when not (String.IsNullOrWhiteSpace port) -> port
        | _ -> link.FromNode

    let private applyRouteLink
        (builder: CardDiagramSlotBuilder.Builder)
        (cardId: string)
        (filterNames: HashSet<string>)
        (link: FlowLinkDef)
        =
        if not (builder.Slots.ContainsKey link.ToNode) then
            raise (
                DashSpecParseException(
                    $"Card '{cardId}': route link target '{link.ToNode}' is not a diagram slot on this card."
                )
            )

        let filterName = routeFilterName link

        if not (filterNames.Contains filterName) then
            raise (
                DashSpecParseException(
                    $"Card '{cardId}': route link references unknown filter '{filterName}'."
                )
            )

        let slot = builder.Slots.[link.ToNode]

        if not (slot.BoundFilters.Contains filterName) then
            slot.BoundFilters.Add filterName

    let private applyModuleFlowLink
        (builder: CardDiagramSlotBuilder.Builder)
        (cardId: string)
        (link: FlowLinkDef)
        =
        if not (builder.Slots.ContainsKey link.ToNode) then
            raise (
                DashSpecParseException(
                    $"Card '{cardId}': flow link target '{link.ToNode}' is not a diagram slot on this card."
                )
            )

        let portName =
            match link.FromPort with
            | Some port when not (String.IsNullOrWhiteSpace port) -> port
            | _ -> "rows"

        let flowInput =
            { Alias = link.ToNode
              NodeId = link.FromNode
              PortName = portName }

        CardDiagramSlotBuilder.applyFlowInput builder link.ToNode flowInput cardId

    let private applyCardInputLink
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
                    $"Card '{cardId}': flow link producer '{link.FromNode}' is not a card input alias; use module link 'node [port] -> [rows] slot' or declare 'input {link.FromNode} from node.port'."
                )
            )

    let private classifyLink
        (filterNames: HashSet<string>)
        (cardInputs: IReadOnlyDictionary<string, CardFlowInput>)
        (link: FlowLinkDef)
        =
        if cardInputs.ContainsKey link.FromNode then
            InputAlias
        elif filterNames.Contains link.FromNode then
            Route
        else
            ModuleFlow

    let private applyLink
        (builder: CardDiagramSlotBuilder.Builder)
        (cardId: string)
        (filterNames: HashSet<string>)
        (cardInputs: IReadOnlyDictionary<string, CardFlowInput>)
        (link: FlowLinkDef)
        =
        match classifyLink filterNames cardInputs link with
        | InputAlias -> applyCardInputLink builder cardId cardInputs link
        | Route -> applyRouteLink builder cardId filterNames link
        | ModuleFlow -> applyModuleFlowLink builder cardId link

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
        (filters: IReadOnlyList<FilterDefinition>)
        (interior: CardInteriorFlowDefinition option)
        (cardInputs: IReadOnlyDictionary<string, CardFlowInput>)
        =
        let filterNames = filterNameSet filters

        match interior with
        | Some flow ->
            for link in flow.Links do
                applyLink builder cardId filterNames cardInputs link
        | None -> ()

        autoWireMatchingInputs builder cardId cardInputs
