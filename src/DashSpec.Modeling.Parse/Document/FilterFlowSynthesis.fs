namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Card
open DashSpec.Modeling.Parse.DataFlow
open DashSpec.Modeling.Parse.Filter

/// <summary>
/// ADR-0120 compile phase: filters transpile into typed param nodes and the
/// card data edges are re-synthesized through them — scope resolved by
/// placement (report toolbar → report-scope node, card local → card-scope
/// node). The plan is additive IR: consumers that ignore it keep the legacy
/// view (BoundFilters + QueryCompiler) unchanged.
/// </summary>
module FilterFlowSynthesis =

    type FilterFlowPlan = FilterNodeSynthesis.FilterFlowPlan

    let private isDashboardFilter (document: DashboardDocument) (name: string) =
        document.DashboardFilters
        |> Seq.exists (fun n -> String.Equals(n, name, StringComparison.OrdinalIgnoreCase))

    let private findFilter (document: DashboardDocument) (name: string) =
        document.Filters
        |> Seq.tryFind (fun f -> String.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))

    /// <summary>Scope resolution by placement: dashboard toolbar names are
    /// report-scope; names on the card's LocalFilters are card-scope. A bound
    /// filter declared nowhere is a loud error — the graph never wires ghosts.</summary>
    let private resolveScope (document: DashboardDocument) (card: CardDefinition) (name: string) =
        if isDashboardFilter document name then
            Some(FilterNodeSynthesis.FilterNodeScope.Report)
        elif
            card.LocalFilters
            |> Seq.exists (fun n -> String.Equals(n, name, StringComparison.OrdinalIgnoreCase))
        then
            Some(FilterNodeSynthesis.FilterNodeScope.Card card.Id)
        else
            None

    /// <summary>Depends validation (loud): the cascade-domain target must be a
    /// declared filter — bare id among the document's filters, `report.&lt;id&gt;`
    /// resolved the same way (report-scope dictionary).</summary>
    let private validateDepends (document: DashboardDocument) (filter: FilterDefinition) =
        match filter.DependsOn with
        | None -> ()
        | Some dependsOn ->
            let targetName =
                if dependsOn.StartsWith("report.", StringComparison.OrdinalIgnoreCase) then
                    dependsOn.["report.".Length ..]
                else
                    dependsOn

            if findFilter document targetName |> Option.isNone then
                raise (
                    DashSpecParseException(
                        $"Filter '{filter.Name}' depends on '{dependsOn}', but no such filter is declared."
                    )
                )

    let private scopeFlowLinks (document: DashboardDocument) =
        let links = ResizeArray<FlowLinkDef>()

        match document.ReportScopeFlow with
        | Some flow -> links.AddRange(FlowGraphSections.linksFor flow.Sections FlowGraphKind.Data)
        | None -> ()

        match document.Pages with
        | None -> ()
        | Some pages ->
            for page in pages do
                match page.ScopeFlow with
                | Some flow -> links.AddRange(FlowGraphSections.linksFor flow.Sections FlowGraphKind.Data)
                | None -> ()

        links :> IReadOnlyList<FlowLinkDef>

    let private edge fromNode fromPort toNode toPort =
        { FromNode = fromNode
          FromPort = fromPort
          ToNode = toNode
          ToPort = toPort }

    /// <summary>Synthesize the plan: nodes for every placed filter, and for
    /// every data link landing on a card anchor, a chain
    /// producer → filter.… → … → anchor through that card's bound filters
    /// (bind order). Links without bound filters pass through as-is so the
    /// edge set stays a complete Data view.</summary>
    let synthesize (document: DashboardDocument) : FilterFlowPlan =
        for filter in document.Filters do
            validateDepends document filter

        let byName = Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase)
        for filter in document.Filters do
            byName.[filter.Name] <- filter

        let nodes = Dictionary<DashflowTransformerDef, unit>(HashIdentity.Reference)
        let nodeById = Dictionary<string, DashflowTransformerDef>(StringComparer.OrdinalIgnoreCase)
        let edges = ResizeArray<FlowLinkDef>()

        let ensureNode (scope: FilterNodeSynthesis.FilterNodeScope) (name: string) =
            let filter =
                match findFilter document name with
                | Some f -> f
                | None -> raise (DashSpecParseException($"Card binds filter '{name}' that is not declared."))

            let node = FilterNodeSynthesis.synthesize scope filter

            if not (nodeById.ContainsKey node.Id) then
                nodeById.[node.Id] <- node
                nodes.Add(node, ())

            node.Id

        let cardById =
            document.Cards
            |> Seq.map (fun c -> c.Id.ToLowerInvariant(), c)
            |> dict

        let targetCardId (toNode: string) =
            if not (FlowPlacementAnchors.isDataFlowCardAnchor toNode) then
                None
            else
                let parts = toNode.Split('.')
                match cardById.TryGetValue(parts.[1].ToLowerInvariant()) with
                | true, card -> Some card
                | false, _ -> None

        for link in scopeFlowLinks document do
            match targetCardId link.ToNode with
            | None -> edges.Add(link)
            | Some card when card.BoundFilters.Count = 0 -> edges.Add(link)
            | Some card ->
                let scopes =
                    card.BoundFilters
                    |> Seq.map (fun name ->
                        match resolveScope document card name with
                        | Some scope -> scope, name
                        | None ->
                            raise (
                                DashSpecParseException(
                                    $"Card '{card.Id}' binds filter '{name}' that is neither a dashboard filter nor a card local filter."
                                )
                            ))
                    |> List.ofSeq

                let nodeIds = scopes |> List.map (fun (scope, name) -> ensureNode scope name)

                let chain =
                    let head = edge link.FromNode link.FromPort (List.head nodeIds) None
                    let middle =
                        nodeIds
                        |> List.pairwise
                        |> List.map (fun (a, b) -> edge a None b None)
                    let last = edge (List.last nodeIds) None link.ToNode link.ToPort
                    head :: middle @ [ last ]

                edges.AddRange chain

        { Nodes = Seq.toArray nodes.Keys
          Edges = edges.ToArray() }

    /// <summary>Document phase: attach the plan (additive field). Runs after
    /// scope flows materialize and before the wiring graph is built.</summary>
    let apply (document: DashboardDocument) =
        let plan = synthesize document
        { document with FilterFlowPlan = Some plan }
