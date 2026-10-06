namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open DashSpec.Modeling.Parse.Card
open DashSpec.Modeling.Parse.DataFlow
open DashSpec.Modeling.Parse.DataSource

/// <summary>Compile card flow inputs to concrete datasources during Modeling (ADR-0048 / ADR-0078).</summary>
module DocumentFlowMaterializer =

    let private isFlowPlaceholder (source: DataSourceDefinition) =
        source.Kind = DataSourceKind.View
        && String.IsNullOrWhiteSpace source.Value
        && not source.ProviderInfer
        && Option.forall String.IsNullOrWhiteSpace source.ProviderId

    let private portRowType (flow: DashflowModule) (nodeId: string) (portName: string) =
        let source =
            flow.Sources
            |> Seq.tryFind (fun s -> String.Equals(s.Id, nodeId, StringComparison.OrdinalIgnoreCase))

        match source with
        | Some s when String.Equals(s.OutputPort, portName, StringComparison.OrdinalIgnoreCase) -> Some s.OutputRowType
        | Some _ -> None
        | None ->
            flow.Transformers
            |> Seq.tryFind (fun t -> String.Equals(t.Id, nodeId, StringComparison.OrdinalIgnoreCase))
            |> Option.bind (fun t ->
                t.Outputs
                |> Seq.tryFind (fun (name, _) -> String.Equals(name, portName, StringComparison.OrdinalIgnoreCase))
                |> Option.map (fun (_, rowType) -> rowType))

    let private toDataSource (source: DashflowSourceDef) (rowsType: string) =
        let providerInfer = DashflowProviderBridge.isInfer source.Provider
        let providerId = DashflowProviderBridge.tryNamedId source.Provider

        match source.From.Kind with
        | SourceFromKind.View ->
            { Kind = DataSourceKind.View
              Value = source.From.Value
              SqlCarrier = None
              Sheet = None
              RowsType = rowsType
              ProviderInfer = providerInfer
              ProviderId = providerId }
        | SourceFromKind.SqlQuery ->
            { Kind = DataSourceKind.Sql
              Value = source.From.Value
              SqlCarrier = Some DataSourceSqlCarrier.Query
              Sheet = None
              RowsType = rowsType
              ProviderInfer = providerInfer
              ProviderId = providerId }
        | SourceFromKind.SqlFile ->
            { Kind = DataSourceKind.Sql
              Value = source.From.Value
              SqlCarrier = Some DataSourceSqlCarrier.File
              Sheet = None
              RowsType = rowsType
              ProviderInfer = providerInfer
              ProviderId = providerId }

    let private toInputRef (input: CardFlowInput) : DashflowPathResolver.CardInputRef =
        { Alias = input.Alias
          NodeId = input.NodeId
          PortName = input.PortName }

    let materializeDataSource (flow: DashflowModule) (input: CardFlowInput) =
        let path = DashflowPathResolver.resolve flow (toInputRef input)
        let rowType =
            portRowType flow input.NodeId input.PortName
            |> Option.defaultValue path.Source.OutputRowType
        toDataSource path.Source rowType

    let private materializeSlot (flow: DashflowModule) (slot: CardDiagramSlot) =
        match slot.FlowInput with
        | None -> slot
        | Some input when not (isFlowPlaceholder slot.DataSource) -> slot
        | Some input ->
            slot
            |> fun s -> { s with DataSource = materializeDataSource flow input }

    let private materializeCard (flow: DashflowModule) (card: CardDefinition) =
        let slots =
            if card.DiagramSlots.Count = 0 then
                card.DiagramSlots
            else
                let built = Dictionary<string, CardDiagramSlot>(StringComparer.OrdinalIgnoreCase)
                for kv in card.DiagramSlots do
                    built.[kv.Key] <- materializeSlot flow kv.Value
                built :> IReadOnlyDictionary<_, _>

        let card' =
            match card.FlowInput with
            | None -> card
            | Some input when not (isFlowPlaceholder card.DataSource) -> card
            | Some input -> { card with DataSource = materializeDataSource flow input }

        let primaryRef =
            match card.DiagramSlotRef with
            | Some ref when not (String.IsNullOrWhiteSpace ref) -> Some ref
            | _ -> card.DiagramSlots.Keys |> Seq.tryHead

        let card'' =
            match primaryRef with
            | Some ref when isFlowPlaceholder card'.DataSource && slots.ContainsKey ref ->
                let primary = slots.[ref]
                if not (isFlowPlaceholder primary.DataSource) then
                    { card' with DataSource = primary.DataSource }
                else
                    card'
            | _ -> card'

        if slots.Count = 0 then
            card''
        else
            { card'' with DiagramSlots = slots }

    /// <summary>Apply flow-backed datasources for every card when a module dashflow is present.</summary>
    let materialize (document: DashboardDocument) =
        match document.Dashflow with
        | None -> document
        | Some flow ->
            let cards = document.Cards |> Seq.map (materializeCard flow) |> Seq.toList
            { document with Cards = cards }
