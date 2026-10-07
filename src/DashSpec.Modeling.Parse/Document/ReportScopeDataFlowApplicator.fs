namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Card
open DashSpec.Modeling.Parse.DataFlow
open DashSpec.Modeling.Parse.Filter

/// <summary>Apply report/page <c>data flow</c> to card diagram slots (ADR-0094).</summary>
module ReportScopeDataFlowApplicator =

    let private filterNameSet (filters: IReadOnlyList<FilterDefinition>) =
        let set = HashSet<string>(StringComparer.OrdinalIgnoreCase)
        for f in filters do
            set.Add f.Name |> ignore
        set

    let private routeFilterName (link: FlowLinkDef) =
        match link.ToPort with
        | Some port when not (String.IsNullOrWhiteSpace port) -> port
        | _ -> link.FromNode

    let private findCardIndex (cards: ResizeArray<CardDefinition>) (cardId: string) =
        cards
        |> Seq.tryFindIndex (fun c -> String.Equals(c.Id, cardId, StringComparison.OrdinalIgnoreCase))

    let private modulePortName (link: FlowLinkDef) =
        match link.FromPort with
        | Some port when not (String.IsNullOrWhiteSpace port) -> port
        | _ -> "rows"

    let private updateSlot
        (card: CardDefinition)
        (slotRef: string)
        (update: CardDiagramSlot -> CardDiagramSlot)
        =
        if not (card.DiagramSlots.ContainsKey slotRef) then
            raise (
                DashSpecParseException(
                    $"Card '{card.Id}': data flow references unknown diagram slot '{slotRef}'."
                )
            )

        let slots = Dictionary<string, CardDiagramSlot>(card.DiagramSlots, StringComparer.OrdinalIgnoreCase)
        slots.[slotRef] <- update slots.[slotRef]
        { card with DiagramSlots = slots :> IReadOnlyDictionary<_, _> }

    let private applyModuleLink (contextLabel: string) (card: CardDefinition) (slotRef: string) (link: FlowLinkDef) =
        let portName = modulePortName link

        let flowInput =
            { Alias = slotRef
              NodeId = link.FromNode
              PortName = portName }

        updateSlot
            card
            slotRef
            (fun slot ->
                if slot.FlowInput.IsSome then
                    raise (
                        DashSpecParseException(
                            $"{contextLabel}: card '{card.Id}' slot '{slotRef}' already has a flow input."
                        )
                    )

                { slot with FlowInput = Some flowInput })

    let private applyRouteLink
        (contextLabel: string)
        (filterNames: HashSet<string>)
        (card: CardDefinition)
        (slotRef: string)
        (link: FlowLinkDef)
        =
        let filterName = routeFilterName link

        if not (filterNames.Contains filterName) then
            raise (
                DashSpecParseException(
                    $"{contextLabel}: data flow references unknown filter '{filterName}'."
                )
            )

        updateSlot
            card
            slotRef
            (fun slot ->
                let bound = ResizeArray(slot.BoundFilters :> seq<_>)

                if not (bound |> Seq.exists (fun n -> String.Equals(n, filterName, StringComparison.OrdinalIgnoreCase))) then
                    bound.Add filterName

                { slot with BoundFilters = bound :> IReadOnlyList<_> })

    let private isFilterRoute (filterNames: HashSet<string>) (link: FlowLinkDef) =
        filterNames.Contains link.FromNode

    let private applyScopeDataLink
        (contextLabel: string)
        (cards: ResizeArray<CardDefinition>)
        (filters: IReadOnlyList<FilterDefinition>)
        (link: FlowLinkDef)
        =
        let cardId, slotRef = CardDataFlowTargets.expectCardSlot contextLabel link.ToNode
        let filterNames = filterNameSet filters

        let cardIndex =
            match findCardIndex cards cardId with
            | None -> raise (DashSpecParseException($"{contextLabel}: data flow references unknown card '{cardId}'."))
            | Some i -> i

        let card = cards.[cardIndex]

        cards.[cardIndex] <-
            if isFilterRoute filterNames link then
                applyRouteLink contextLabel filterNames card slotRef link
            else
                applyModuleLink contextLabel card slotRef link

    let private applySections
        (contextLabel: string)
        (cards: ResizeArray<CardDefinition>)
        (filters: IReadOnlyList<FilterDefinition>)
        (sections: FlowGraphSections)
        =
        for link in FlowGraphSections.linksFor sections FlowGraphKind.Data do
            applyScopeDataLink contextLabel cards filters link

    let private syncPrimaryFlowInput (card: CardDefinition) =
        let primaryRef =
            match card.DiagramSlotRef with
            | Some ref when not (String.IsNullOrWhiteSpace ref) -> Some ref
            | _ -> card.DiagramSlots.Keys |> Seq.tryHead

        match primaryRef with
        | None -> card
        | Some ref ->
            match card.DiagramSlots.TryGetValue ref with
            | true, slot when slot.FlowInput.IsSome -> { card with FlowInput = slot.FlowInput }
            | _ -> card

    let private validateCards (cards: ResizeArray<CardDefinition>) =
        for i in 0 .. cards.Count - 1 do
            let card = syncPrimaryFlowInput cards.[i]
            cards.[i] <- card

            for KeyValue(slotRef, slot) in card.DiagramSlots do
                if slot.FlowInput.IsNone && String.IsNullOrWhiteSpace slot.DataSource.Value then
                    raise (
                        DashSpecParseException(
                            $"Card '{card.Id}' slot '{slotRef}': no module data flow (report/page/card data flow -> card.{card.Id}.{slotRef})."
                        )
                    )

    let apply (shell: DashboardShellContext) =
        match shell.ReportScopeFlow with
        | Some flow -> applySections "Report" shell.Cards (shell.Filters :> IReadOnlyList<_>) flow.Sections
        | None -> ()

        for page in shell.Pages |> Seq.toList do
            match page.ScopeFlow with
            | Some flow ->
                applySections $"Page '{page.Id}'" shell.Cards (shell.Filters :> IReadOnlyList<_>) flow.Sections
            | None -> ()

        validateCards shell.Cards
