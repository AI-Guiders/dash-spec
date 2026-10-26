namespace DashSpec.Modeling.Parse.Card

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.DataSource
open DashSpec.Modeling.Parse.Diagram
open DashSpec.Modeling.Parse.Presentation
open DashSpec.Modeling.Parse.Transform
open DashSpec.Modeling.Parse.Layout

module CardDiagramSlotBuilder =

    let internal DiagramSlotId = "__diagram__"

    type PayloadScratch =
        { Name: string
          Diagram: DiagramDefinition
          mutable FlowInput: CardFlowInput option }

    type SlotScratch =
        { mutable Diagram: DiagramDefinition option
          mutable DataSource: DataSourceDefinition option
          BoundFilters: ResizeArray<string>
          mutable Legend: LegendDefinition option
          mutable Presentation: PresentationBlock option
          mutable SeriesTransform: SeriesTransformBlock option
          mutable FlowInput: CardFlowInput option
          Payloads: ResizeArray<PayloadScratch> }

    type Builder =
        { Slots: Dictionary<string, SlotScratch>
          Order: ResizeArray<string>
          mutable PrimarySlotRef: string option }

    let create () =
        { Slots = Dictionary<string, SlotScratch>(StringComparer.OrdinalIgnoreCase)
          Order = ResizeArray<string>()
          PrimarySlotRef = None }

    let private ensure (builder: Builder) (slotRef: string) =
        if not (builder.Slots.ContainsKey slotRef) then
            builder.Slots.[slotRef] <-
                { Diagram = None
                  DataSource = None
                  BoundFilters = ResizeArray<string>()
                  Legend = None
                  Presentation = None
                  SeriesTransform = None
                  FlowInput = None
                  Payloads = ResizeArray<PayloadScratch>() }
            builder.Order.Add slotRef

        if builder.PrimarySlotRef.IsNone then
            builder.PrimarySlotRef <- Some slotRef

        builder.Slots.[slotRef]

    let ensureSlot (builder: Builder) (slotRef: string) = ensure builder slotRef

    let resolveSlotRef (builder: Builder) (explicitRef: string option) =
        match explicitRef with
        | Some r when not (String.IsNullOrWhiteSpace r) -> r
        | _ -> DiagramSlotId

    let touchDiagram
        (builder: Builder)
        (slotRef: string)
        (diagram: DiagramDefinition)
        (legend: LegendDefinition option)
        (presentation: PresentationBlock option)
        (seriesTransform: SeriesTransformBlock option)
        =
        let slot = ensure builder slotRef
        slot.Diagram <- Some diagram
        if legend.IsSome then slot.Legend <- legend
        if presentation.IsSome then slot.Presentation <- presentation
        if seriesTransform.IsSome then slot.SeriesTransform <- seriesTransform

    let applyData
        (builder: Builder)
        (slotRef: string)
        (dataSource: DataSourceDefinition)
        (boundFilters: IReadOnlyList<string>)
        =
        let slot = ensure builder slotRef
        slot.DataSource <- Some dataSource
        slot.BoundFilters.Clear()
        slot.BoundFilters.AddRange boundFilters

    let applyFlowInput (builder: Builder) (slotRef: string) (input: CardFlowInput) (cardId: string) =
        let slot = ensure builder slotRef
        if slot.FlowInput.IsSome then
            raise (DashSpecParseException($"Card '{cardId}': diagram slot '{slotRef}' declares more than one flow input."))
        slot.FlowInput <- Some input

    /// ADR-0113: `payload <name> <diagram>` line on a switch slot. A slot is either
    /// classic (one `diagram ref <slot> <name>`) or a switch slot (payload lines) — not both.
    let addPayload (builder: Builder) (slotRef: string) (payloadName: string) (diagram: DiagramDefinition) (cardId: string) =
        let slot = ensure builder slotRef
        if slot.Diagram.IsSome then
            raise (
                DashSpecParseException(
                    $"Card '{cardId}': diagram slot '{slotRef}' mixes a diagram name and payload lines; use one form.")
            )
        if slot.Payloads.Exists (fun p -> String.Equals(p.Name, payloadName, StringComparison.OrdinalIgnoreCase)) then
            raise (
                DashSpecParseException($"Card '{cardId}': diagram slot '{slotRef}' declares payload '{payloadName}' twice.")
            )
        slot.Payloads.Add { Name = payloadName; Diagram = diagram; FlowInput = None }

    /// ADR-0113: `data flow … -> [rows] <slot>.<payload>` binds one payload of a switch slot.
    let applyPayloadFlowInput
        (builder: Builder)
        (slotRef: string)
        (payloadName: string)
        (input: CardFlowInput)
        (cardId: string)
        =
        let slot = ensure builder slotRef
        match slot.Payloads |> Seq.tryFind (fun p -> String.Equals(p.Name, payloadName, StringComparison.OrdinalIgnoreCase)) with
        | Some payload ->
            if payload.FlowInput.IsSome then
                raise (
                    DashSpecParseException(
                        $"Card '{cardId}': diagram slot '{slotRef}' payload '{payloadName}' declares more than one flow input.")
                )
            payload.FlowInput <- Some input
        | None ->
            let declared =
                if slot.Payloads.Count = 0 then "slot is not a switch slot"
                else String.Join(", ", slot.Payloads |> Seq.map (fun p -> p.Name))
            raise (
                DashSpecParseException(
                    $"Card '{cardId}': flow link target '{slotRef}.{payloadName}' names an unknown payload ({declared}).")
            )

    let pruneDataOnlyDiagramSlot (builder: Builder) =
        let diagramSlotCount =
            builder.Slots.Values |> Seq.filter (fun s -> s.Diagram.IsSome) |> Seq.length

        if diagramSlotCount >= 2 then
            ()
        elif builder.Slots.ContainsKey DiagramSlotId && builder.Order.Count > 1 then
            let named =
                builder.Order
                |> Seq.filter (fun slotRef -> not (String.Equals(slotRef, DiagramSlotId, StringComparison.Ordinal)))
                |> Seq.toList
            if named.Length = 1 then
                let target = named.[0]
                let orphan = builder.Slots.[DiagramSlotId]
                let targetScratch = builder.Slots.[target]
                if orphan.Diagram.IsNone then
                    if targetScratch.DataSource.IsNone && orphan.DataSource.IsSome then
                        targetScratch.DataSource <- orphan.DataSource
                    if targetScratch.BoundFilters.Count = 0 && orphan.BoundFilters.Count > 0 then
                        targetScratch.BoundFilters.AddRange orphan.BoundFilters
                    builder.Slots.Remove DiagramSlotId |> ignore
                    let idx = builder.Order.IndexOf DiagramSlotId
                    if idx >= 0 then
                        builder.Order.RemoveAt idx

    let unboundFlowDataSource =
        { Kind = DataSourceKind.View
          Value = ""
          SqlCarrier = None
          Sheet = None
          RowsType = ""
          ProviderInfer = false
          ProviderId = None }

    let private resolveSlotFlowInput (scratch: SlotScratch) (cardFlowInput: CardFlowInput option) =
        match scratch.FlowInput with
        | Some fi -> Some fi
        | None -> cardFlowInput

    let build (builder: Builder) (cardId: string) (flowInput: CardFlowInput option) =
        if builder.Slots.Count = 0 then
            None
        else
            let built = Dictionary<string, CardDiagramSlot>(StringComparer.OrdinalIgnoreCase)
            for slotRef in builder.Order do
                let scratch = builder.Slots.[slotRef]
                let slotFlowInput = resolveSlotFlowInput scratch flowInput
                if scratch.Payloads.Count > 0 then
                    // ADR-0113 switch slot: the head payload supplies the classic Diagram field
                    // (kind resolution, awaiting state); per-payload flow inputs inherit the
                    // slot-level one when they have none of their own.
                    let payloads =
                        [
                            for p in scratch.Payloads ->
                                { Name = p.Name
                                  Diagram = p.Diagram
                                  FlowInput = match p.FlowInput with Some fi -> Some fi | None -> slotFlowInput } : CardSlotPayload
                        ]
                        :> IReadOnlyList<CardSlotPayload>
                    built.[slotRef] <-
                        { SlotRef = slotRef
                          Diagram = scratch.Payloads.[0].Diagram
                          DataSource = unboundFlowDataSource
                          BoundFilters = scratch.BoundFilters :> IReadOnlyList<_>
                          Legend = scratch.Legend
                          Presentation = scratch.Presentation
                          SeriesTransform = scratch.SeriesTransform
                          FlowInput = slotFlowInput
                          Payloads = payloads }
                else
                    let flowBacked = Option.isSome slotFlowInput
                    match scratch.Diagram, scratch.DataSource with
                    | Some diagram, Some dataSource ->
                        built.[slotRef] <-
                            { SlotRef = slotRef
                              Diagram = diagram
                              DataSource = dataSource
                              BoundFilters = scratch.BoundFilters :> IReadOnlyList<_>
                              Legend = scratch.Legend
                              Presentation = scratch.Presentation
                              SeriesTransform = scratch.SeriesTransform
                              FlowInput = slotFlowInput
                              Payloads = [] }
                    | Some diagram, None when flowBacked ->
                        built.[slotRef] <-
                            { SlotRef = slotRef
                              Diagram = diagram
                              DataSource = unboundFlowDataSource
                              BoundFilters = scratch.BoundFilters :> IReadOnlyList<_>
                              Legend = scratch.Legend
                              Presentation = scratch.Presentation
                              SeriesTransform = scratch.SeriesTransform
                              FlowInput = slotFlowInput
                              Payloads = [] }
                    | None, _ ->
                        raise (DashSpecParseException($"Card '{cardId}': diagram slot '{slotRef}' requires a diagram."))
                    | Some diagram, None ->
                        built.[slotRef] <-
                            { SlotRef = slotRef
                              Diagram = diagram
                              DataSource = unboundFlowDataSource
                              BoundFilters = scratch.BoundFilters :> IReadOnlyList<_>
                              Legend = scratch.Legend
                              Presentation = scratch.Presentation
                              SeriesTransform = scratch.SeriesTransform
                              FlowInput = slotFlowInput
                              Payloads = [] }

            Some(built :> IReadOnlyDictionary<_, _>)

    let private layoutTokenToSlotRef (token: string) =
        if String.Equals(token, "diagram", StringComparison.OrdinalIgnoreCase) then DiagramSlotId
        else token

    let resolvePrimarySlotKey (builder: Builder) (interior: LayoutBoardDefinition option) =
        let tryFromBoard (board: LayoutBoardDefinition) =
            let mutable found = None
            for row in board.Rows do
                for token in row do
                    if found.IsNone then
                        let slotRef = layoutTokenToSlotRef token
                        if builder.Slots.ContainsKey slotRef then
                            let scratch = builder.Slots.[slotRef]
                            if scratch.Diagram.IsSome then
                                found <- Some slotRef
            found

        match interior with
        | Some board ->
            match tryFromBoard board with
            | Some key -> key
            | None ->
                if builder.Order.Count > 0 then builder.Order.[0]
                else DiagramSlotId
        | None ->
            if builder.Order.Count > 0 then builder.Order.[0]
            else DiagramSlotId

    let synthesizeInteriorRows (builder: Builder) (localFilters: IReadOnlyList<string>) =
        let rows = ResizeArray<IReadOnlyList<string>>()
        if localFilters.Count > 0 then
            rows.Add(localFilters)
        for slotRef in builder.Order do
            let token =
                if String.Equals(slotRef, DiagramSlotId, StringComparison.Ordinal) then "diagram"
                else slotRef
            rows.Add([ token ])
        rows :> IReadOnlyList<_>
