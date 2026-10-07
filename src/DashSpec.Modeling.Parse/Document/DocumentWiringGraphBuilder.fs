namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Card
open DashSpec.Modeling.Parse.DataFlow
open DashSpec.Modeling.Parse.Filter
open DashSpec.Modeling.Parse.Layout

/// <summary>Compile unified <see cref="WiringGraph"/> from a parsed dashboard (ADR-0091 / ADR-0092).</summary>
module DocumentWiringGraphBuilder =

    let private chromeDashboard = "chrome.dashboard"
    let private chromeCard (cardId: string) = $"chrome.card.{cardId}"

    let private addNode (nodes: Dictionary<string, WiringNode>) (id: string) (kind: WiringNodeKind) (scope: string option) =
        if not (nodes.ContainsKey id) then
            nodes.[id] <- { Id = id; Kind = kind; Scope = scope }

    let private addEdge (edges: ResizeArray<WiringEdge>) (edge: WiringEdge) = edges.Add edge

    let private classifyScopeLink (link: FlowLinkDef) =
        let toLower = link.ToNode.ToLowerInvariant()

        if toLower.StartsWith("chrome.") || toLower.StartsWith("host.") then
            WiringEdgeKind.Route
        elif link.FromPort |> Option.exists (fun p -> p.Contains("click", StringComparison.OrdinalIgnoreCase)) then
            WiringEdgeKind.Event
        elif toLower.StartsWith("phase.") || toLower.StartsWith("page.") then
            WiringEdgeKind.Event
        else
            WiringEdgeKind.Route

    let private lowerDashboardToolbar
        (nodes: Dictionary<string, WiringNode>)
        (edges: ResizeArray<WiringEdge>)
        (filters: IReadOnlyList<FilterDefinition>)
        (dashboardFilters: IReadOnlyList<string>)
        =
        let scope = Some "report"
        addNode nodes chromeDashboard WiringNodeKind.Chrome scope

        for filterName in dashboardFilters do
            addNode nodes filterName WiringNodeKind.Filter scope

            addEdge
                edges
                { From = filterName
                  FromPort = None
                  To = chromeDashboard
                  ToPort = Some "toolbar"
                  Kind = WiringEdgeKind.Route
                  Scope = scope }

    let private lowerPageToolbar
        (nodes: Dictionary<string, WiringNode>)
        (edges: ResizeArray<WiringEdge>)
        (filters: IReadOnlyList<FilterDefinition>)
        (page: ReportPageDefinition)
        =
        match page.ToolbarBoard with
        | None -> ()
        | Some board ->
            let scope = Some $"page:{page.Id}"
            let chromeId = $"chrome.page.{page.Id}"
            addNode nodes chromeId WiringNodeKind.Chrome scope
            addNode nodes $"page.{page.Id}" WiringNodeKind.Page scope

            for row in board.Rows do
                for token in row do
                    let boardRef, _ = LayoutBoardCellParser.parseCell token $"Page '{page.Id}' toolbar" 1
                    let filterName = FilterLayoutRefResolver.resolve boardRef filters $"Page '{page.Id}' toolbar"
                    addNode nodes filterName WiringNodeKind.Filter scope

                    addEdge
                        edges
                        { From = filterName
                          FromPort = None
                          To = chromeId
                          ToPort = Some "toolbar"
                          Kind = WiringEdgeKind.Route
                          Scope = scope }

    let private lowerPageDerive (nodes: Dictionary<string, WiringNode>) (edges: ResizeArray<WiringEdge>) (page: ReportPageDefinition) =
        match page.UsageDateDerive with
        | None -> ()
        | Some derive ->
            let scope = Some $"page:{page.Id}"
            addNode nodes derive.SourceFilter WiringNodeKind.Filter scope
            addNode nodes derive.TargetFilter WiringNodeKind.Filter scope

            addEdge
                edges
                { From = derive.SourceFilter
                  FromPort = None
                  To = derive.TargetFilter
                  ToPort = derive.GrainFilterName
                  Kind = WiringEdgeKind.Route
                  Scope = scope }

    let private lowerCardFilterPanel
        (nodes: Dictionary<string, WiringNode>)
        (edges: ResizeArray<WiringEdge>)
        (card: CardDefinition)
        =
        if card.LocalFilters.Count = 0 then
            ()
        else
            let scope = Some $"card:{card.Id}"
            addNode nodes (chromeCard card.Id) WiringNodeKind.Chrome scope
            addNode nodes card.Id WiringNodeKind.Card scope

            for filterName in card.LocalFilters do
                addNode nodes filterName WiringNodeKind.Filter scope

                addEdge
                    edges
                    { From = filterName
                      FromPort = None
                      To = chromeCard card.Id
                      ToPort = Some "toolbar"
                      Kind = WiringEdgeKind.Route
                      Scope = scope }

    let private lowerCardHost
        (nodes: Dictionary<string, WiringNode>)
        (edges: ResizeArray<WiringEdge>)
        (card: CardDefinition)
        =
        match card.FilterHostCardId, card.HostedFilters with
        | Some hostId, Some hosted ->
            let scope = Some $"card:{card.Id}"
            addNode nodes hostId WiringNodeKind.Card scope
            addNode nodes card.Id WiringNodeKind.Card scope
            addNode nodes (chromeCard card.Id) WiringNodeKind.Chrome scope

            for filterName in hosted do
                addNode nodes filterName WiringNodeKind.Filter scope

                addEdge
                    edges
                    { From = hostId
                      FromPort = Some filterName
                      To = chromeCard card.Id
                      ToPort = Some "host"
                      Kind = WiringEdgeKind.Route
                      Scope = scope }
        | _ -> ()

    let private lowerCardInterior
        (nodes: Dictionary<string, WiringNode>)
        (edges: ResizeArray<WiringEdge>)
        (card: CardDefinition)
        (filters: IReadOnlyList<FilterDefinition>)
        =
        match card.InteriorFlow with
        | None -> ()
        | Some interior ->
            let scope = Some $"card:{card.Id}"
            let filterNames = HashSet<string>(filters |> Seq.map (fun f -> f.Name), StringComparer.OrdinalIgnoreCase)

            for link in interior.Links do
                let kind =
                    if filterNames.Contains link.FromNode then
                        WiringEdgeKind.Route
                    else
                        WiringEdgeKind.Flow

                if kind = WiringEdgeKind.Flow then
                    addNode nodes link.FromNode WiringNodeKind.ModuleNode scope

                addNode nodes link.ToNode WiringNodeKind.Slot scope

                addEdge
                    edges
                    { From = link.FromNode
                      FromPort = link.FromPort
                      To = link.ToNode
                      ToPort = link.ToPort
                      Kind = kind
                      Scope = scope }

    let private lowerCardClicks
        (nodes: Dictionary<string, WiringNode>)
        (edges: ResizeArray<WiringEdge>)
        (card: CardDefinition)
        =
        match card.ClickBehaviour with
        | None -> ()
        | Some behaviour ->
            let scope = Some $"card:{card.Id}"
            let slotRef = card.DiagramSlotRef |> Option.defaultValue card.Id
            let slotNode = $"{card.Id}.{slotRef}"
            addNode nodes slotNode WiringNodeKind.Slot scope
            addNode nodes card.Id WiringNodeKind.Card scope

            for effect in behaviour.Effects do
                match effect with
                | SetFilterFromField(filterName, field) ->
                    addNode nodes filterName WiringNodeKind.Filter scope

                    addEdge
                        edges
                        { From = slotNode
                          FromPort = Some $"click.{field}"
                          To = filterName
                          ToPort = Some "value"
                          Kind = WiringEdgeKind.Event
                          Scope = scope }
                | FocusPhase phaseId ->
                    let pageKey = card.PageId |> Option.defaultValue "report"
                    let phaseNode = $"phase.{pageKey}.{phaseId}"
                    addNode nodes phaseNode WiringNodeKind.Phase scope

                    addEdge
                        edges
                        { From = slotNode
                          FromPort = Some "click"
                          To = phaseNode
                          ToPort = None
                          Kind = WiringEdgeKind.Event
                          Scope = scope }
                | GotoPage pageId ->
                    let pageNode = $"page.{pageId}"
                    addNode nodes pageNode WiringNodeKind.Page scope

                    addEdge
                        edges
                        { From = slotNode
                          FromPort = Some "click"
                          To = pageNode
                          ToPort = None
                          Kind = WiringEdgeKind.Event
                          Scope = scope }
                | DrillTableFromCell bindings ->
                    for filterName, field in bindings do
                        addNode nodes filterName WiringNodeKind.Filter scope

                        addEdge
                            edges
                            { From = slotNode
                              FromPort = Some $"click.{field}"
                              To = filterName
                              ToPort = Some "value"
                              Kind = WiringEdgeKind.Event
                              Scope = scope }
                | _ -> ()

    let private addScopeFlowLinks (edges: ResizeArray<WiringEdge>) (links: IReadOnlyList<FlowLinkDef>) (scope: string option) =
        for link in links do
            let kind = classifyScopeLink link

            addEdge
                edges
                { From = link.FromNode
                  FromPort = link.FromPort
                  To = link.ToNode
                  ToPort = link.ToPort
                  Kind = kind
                  Scope = scope }

    let private lowerModuleDashflow
        (nodes: Dictionary<string, WiringNode>)
        (edges: ResizeArray<WiringEdge>)
        (dashflow: DashflowModule option)
        =
        match dashflow with
        | None -> ()
        | Some flow ->
            let scope = Some "module"

            for link in flow.Links do
                addNode nodes link.FromNode WiringNodeKind.ModuleNode scope
                addNode nodes link.ToNode WiringNodeKind.ModuleNode scope

                addEdge
                    edges
                    { From = link.FromNode
                      FromPort = link.FromPort
                      To = link.ToNode
                      ToPort = link.ToPort
                      Kind = WiringEdgeKind.Flow
                      Scope = scope }

    let build (document: DashboardDocument) : WiringGraph =
        let nodes = Dictionary<string, WiringNode>(StringComparer.OrdinalIgnoreCase)
        let edges = ResizeArray<WiringEdge>()

        for filter in document.Filters do
            addNode nodes filter.Name WiringNodeKind.Filter (Some "report")

        for card in document.Cards do
            addNode nodes card.Id WiringNodeKind.Card (Some $"card:{card.Id}")

        lowerDashboardToolbar nodes edges document.Filters document.DashboardFilters
        lowerModuleDashflow nodes edges document.Dashflow

        for card in document.Cards do
            lowerCardFilterPanel nodes edges card
            lowerCardHost nodes edges card
            lowerCardInterior nodes edges card document.Filters
            lowerCardClicks nodes edges card

        match document.Pages with
        | None -> ()
        | Some pages ->
            for page in pages do
                lowerPageToolbar nodes edges document.Filters page
                lowerPageDerive nodes edges page

                match page.ScopeFlow with
                | Some flow -> addScopeFlowLinks edges flow.Links (Some $"page:{page.Id}")
                | None -> ()

        match document.ReportScopeFlow with
        | Some flow -> addScopeFlowLinks edges flow.Links (Some "report")
        | None -> ()

        WiringGraph.create nodes.Values edges
