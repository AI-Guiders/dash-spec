namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Card
open DashSpec.Modeling.Parse.DataFlow
open DashSpec.Modeling.Parse.Layout

/// <summary>Materialize report/page <c>flow</c> route links into runtime fields (ADR-0092).</summary>
module ReportScopeFlowApplicator =

    let private filterNameFromLink (link: FlowLinkDef) =
        match link.FromPort with
        | Some port when not (String.IsNullOrWhiteSpace port) -> link.FromNode, port
        | _ ->
            let idx = link.FromNode.IndexOf('.')

            if idx > 0 then
                link.FromNode.Substring(0, idx), link.FromNode.Substring(idx + 1)
            else
                link.FromNode, link.FromNode

    let private tryHostConsumer (toNode: string) =
        let parts = toNode.Split('.', StringSplitOptions.RemoveEmptyEntries)

        if parts.Length >= 4
           && String.Equals(parts.[0], "host", StringComparison.OrdinalIgnoreCase)
           && String.Equals(parts.[1], "chrome", StringComparison.OrdinalIgnoreCase)
           && String.Equals(parts.[2], "card", StringComparison.OrdinalIgnoreCase) then
            Some parts.[3]
        else
            None

    let private tryChromeCard (toNode: string) =
        let prefix = "chrome.card."

        if toNode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) then
            Some(toNode.Substring prefix.Length)
        else
            None

    let private tryChromePage (toNode: string) =
        let prefix = "chrome.page."

        if toNode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) then
            Some(toNode.Substring prefix.Length)
        else
            None

    let private isChromeDashboard (toNode: string) =
        String.Equals(toNode, "chrome.dashboard", StringComparison.OrdinalIgnoreCase)

    let private portIs (link: FlowLinkDef) (name: string) =
        match link.ToPort with
        | Some port -> String.Equals(port, name, StringComparison.OrdinalIgnoreCase)
        | None -> false

    let private appendUnique (list: ResizeArray<string>) (name: string) =
        if not (list |> Seq.exists (fun n -> String.Equals(n, name, StringComparison.OrdinalIgnoreCase))) then
            list.Add name

    let private applyToolbarToDashboard (shell: DashboardShellContext) (filterName: string) =
        appendUnique shell.DashboardFilters filterName

    let private applyToolbarToPage (pages: ResizeArray<ReportPageDefinition>) (pageId: string) (filterName: string) =
        let index =
            pages |> Seq.tryFindIndex (fun p -> String.Equals(p.Id, pageId, StringComparison.OrdinalIgnoreCase))

        match index with
        | None -> raise (DashSpecParseException($"Page toolbar route references unknown page '{pageId}'."))
        | Some i ->
            let page = pages.[i]
            let names = ResizeArray<string>()

            match page.ToolbarBoard with
            | Some board ->
                for row in board.Rows do
                    for token in row do
                        names.Add token
            | None -> ()

            appendUnique names filterName

            pages.[i] <-
                { page with
                    ToolbarBoard = Some(ToolbarBoardFactory.fromFilterNames(names :> IReadOnlyList<_>)) }

    let private applyPanelToCard (cards: ResizeArray<CardDefinition>) (cardId: string) (filterName: string) =
        let index =
            cards |> Seq.tryFindIndex (fun c -> String.Equals(c.Id, cardId, StringComparison.OrdinalIgnoreCase))

        match index with
        | None -> raise (DashSpecParseException($"Card panel route references unknown card '{cardId}'."))
        | Some i ->
            let card = cards.[i]
            let names = ResizeArray(card.LocalFilters :> seq<_>)
            appendUnique names filterName

            cards.[i] <-
                { card with
                    LocalFilters = names :> IReadOnlyList<_> }

    let private applyHostLink (cards: ResizeArray<CardDefinition>) (link: FlowLinkDef) =
        match tryHostConsumer link.ToNode with
        | None -> ()
        | Some consumerId ->
            let hostCardId, filterName = filterNameFromLink link

            if String.Equals(hostCardId, filterName, StringComparison.OrdinalIgnoreCase) then
                raise (DashSpecParseException($"Host route '{link.FromNode} -> {link.ToNode}' requires hostCard.filter producer."))

            let consumerIndex =
                cards
                |> Seq.tryFindIndex (fun c -> String.Equals(c.Id, consumerId, StringComparison.OrdinalIgnoreCase))

            match consumerIndex with
            | None ->
                raise (DashSpecParseException($"Host route targets unknown card '{consumerId}'."))
            | Some index ->
                let consumer = cards.[index]

                match consumer.FilterHostCardId with
                | Some existing when not (String.Equals(existing, hostCardId, StringComparison.OrdinalIgnoreCase)) ->
                    raise (
                        DashSpecParseException(
                            $"Card '{consumerId}': conflicting host cards '{existing}' and '{hostCardId}' in flow routes."
                        )
                    )
                | _ -> ()

                let hosted =
                    match consumer.HostedFilters with
                    | Some list -> ResizeArray(list)
                    | None -> ResizeArray()

                appendUnique hosted filterName

                cards.[index] <-
                    { consumer with
                        FilterHostCardId = Some hostCardId
                        HostedFilters = Some(hosted :> IReadOnlyList<_>) }

    let private applyRouteLink (shell: DashboardShellContext) (cards: ResizeArray<CardDefinition>) (link: FlowLinkDef) =
        if tryHostConsumer link.ToNode |> Option.isSome then
            applyHostLink cards link
        elif portIs link "toolbar" then
            let _, filterName = filterNameFromLink link

            if isChromeDashboard link.ToNode then
                applyToolbarToDashboard shell filterName
            else
                match tryChromePage link.ToNode with
                | Some pageId -> applyToolbarToPage shell.Pages pageId filterName
                | None ->
                    raise (
                        DashSpecParseException(
                            $"Toolbar route target '{link.ToNode}' must be chrome.dashboard or chrome.page.<pageId>."
                        )
                    )
        elif portIs link "panel" then
            let _, filterName = filterNameFromLink link

            match tryChromeCard link.ToNode with
            | Some cardId -> applyPanelToCard cards cardId filterName
            | None ->
                raise (DashSpecParseException($"Panel route target '{link.ToNode}' must be chrome.card.<cardId>."))
        else
            ()

    let private applyLinks (shell: DashboardShellContext) (links: IReadOnlyList<FlowLinkDef>) =
        for link in links do
            applyRouteLink shell shell.Cards link

    let apply (shell: DashboardShellContext) =
        match shell.ReportScopeFlow with
        | Some flow -> applyLinks shell flow.Links
        | None -> ()

        for page in shell.Pages do
            match page.ScopeFlow with
            | Some flow -> applyLinks shell flow.Links
            | None -> ()

        let interiorLinkBatches =
            shell.Cards
            |> Seq.choose (fun c -> c.InteriorFlow |> Option.map (fun interior -> interior.Links))
            |> Seq.toList

        for links in interiorLinkBatches do
            applyLinks shell links
