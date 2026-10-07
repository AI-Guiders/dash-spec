namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Card
open DashSpec.Modeling.Parse.DataFlow
open DashSpec.Modeling.Parse.Layout

/// <summary>Materialize show/wire qualified flow into runtime fields (ADR-0092 / ADR-0093).</summary>
module ReportScopeFlowApplicator =

    let private routingLinks (sections: FlowGraphSections) =
        let merged = ResizeArray<FlowLinkDef>()

        for kind in [| FlowGraphKind.Show; FlowGraphKind.Wire; FlowGraphKind.Action |] do
            for link in FlowGraphSections.linksFor sections kind do
                merged.Add link

        merged :> IReadOnlyList<_>

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

    let internal applyToolbarToDashboard (shell: ReportCompileContext) (filterName: string) =
        appendUnique shell.DashboardFilters filterName

    let internal applyToolbarToPage (pages: ResizeArray<ReportPageDefinition>) (pageId: string) (filterName: string) =
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

    let private tryCardFilterInPort (link: FlowLinkDef) =
        match link.ToPort with
        | Some port when port.StartsWith("card.", StringComparison.OrdinalIgnoreCase) ->
            let suffix = port.Substring("card.".Length)

            if String.IsNullOrWhiteSpace suffix then
                raise (DashSpecParseException("Card filter port '[card.<filterId>]' requires a filter id after 'card.'."))
            else
                Some suffix
        | _ -> None

    let internal applyCardToolbarFilter (cards: ResizeArray<CardDefinition>) (cardId: string) (filterName: string) =
        let index =
            cards |> Seq.tryFindIndex (fun c -> String.Equals(c.Id, cardId, StringComparison.OrdinalIgnoreCase))

        match index with
        | None -> raise (DashSpecParseException($"Card toolbar route references unknown card '{cardId}'."))
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

    let private applyRouteLink (shell: ReportCompileContext) (cards: ResizeArray<CardDefinition>) (link: FlowLinkDef) =
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
                    match tryChromeCard link.ToNode with
                    | Some cardId -> applyCardToolbarFilter cards cardId filterName
                    | None ->
                        raise (
                            DashSpecParseException(
                                $"Toolbar route target '{link.ToNode}' must be chrome.dashboard, chrome.page.<pageId>, or chrome.card.<cardId>."
                            )
                        )
        elif portIs link "panel" then
            let _, filterName = filterNameFromLink link

            match tryChromeCard link.ToNode with
            | Some cardId -> applyCardToolbarFilter cards cardId filterName
            | None ->
                raise (
                    DashSpecParseException(
                        $"Deprecated [panel] port: use [toolbar] chrome.card.<cardId> or [card.<filterId>] chrome.card.<cardId>."
                    )
                )
        else
            match tryChromeCard link.ToNode, tryCardFilterInPort link with
            | Some cardId, Some portFilterId ->
                let _, filterName = filterNameFromLink link

                if not (String.Equals(portFilterId, filterName, StringComparison.OrdinalIgnoreCase)) then
                    raise (
                        DashSpecParseException(
                            $"Card filter port '[card.{portFilterId}]' must match filter producer '{filterName}'."
                        )
                    )

                applyCardToolbarFilter cards cardId filterName
            | _, _ -> ()

    let private applyLinks (shell: ReportCompileContext) (links: IReadOnlyList<FlowLinkDef>) =
        let linksSnapshot = links |> Seq.toList
        for link in linksSnapshot do
            applyRouteLink shell shell.Cards link

    let apply (shell: ReportCompileContext) =
        match shell.ReportScopeFlow with
        | Some flow -> applyLinks shell (routingLinks flow.Sections)
        | None -> ()

        // Snapshot: applyToolbarToPage mutates shell.Pages while wiring toolbar routes.
        for page in shell.Pages |> Seq.toList do
            match page.ScopeFlow with
            | Some flow -> applyLinks shell (routingLinks flow.Sections)
            | None -> ()

        let interiorLinkBatches =
            shell.Cards
            |> Seq.toList
            |> Seq.choose (fun c ->
                c.InteriorFlow
                |> Option.map (fun interior -> routingLinks interior.Sections))
            |> Seq.toList

        for links in interiorLinkBatches do
            applyLinks shell links
