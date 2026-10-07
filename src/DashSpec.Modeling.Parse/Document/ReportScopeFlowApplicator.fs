namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Card
open DashSpec.Modeling.Parse.DataFlow
open DashSpec.Modeling.Parse.Filter

/// <summary>Apply explicit report/page scope flow links onto legacy card fields (host routes).</summary>
module ReportScopeFlowApplicator =

    let private tryParseHostConsumer (toNode: string) =
        let parts = toNode.Split('.', StringSplitOptions.RemoveEmptyEntries)

        if parts.Length >= 4
           && String.Equals(parts.[0], "host", StringComparison.OrdinalIgnoreCase)
           && String.Equals(parts.[1], "chrome", StringComparison.OrdinalIgnoreCase)
           && String.Equals(parts.[2], "card", StringComparison.OrdinalIgnoreCase) then
            Some parts.[3]
        elif parts.Length >= 3
           && String.Equals(parts.[0], "chrome", StringComparison.OrdinalIgnoreCase)
           && String.Equals(parts.[1], "card", StringComparison.OrdinalIgnoreCase) then
            Some parts.[2]
        else
            None

    let private hostFilterFromLink (link: FlowLinkDef) =
        match link.FromPort with
        | Some port when not (String.IsNullOrWhiteSpace port) -> link.FromNode, port
        | _ ->
            let idx = link.FromNode.IndexOf('.')

            if idx <= 0 then
                raise (
                    DashSpecParseException(
                        $"Host route link '{link.FromNode} -> {link.ToNode}' requires hostCard.filter producer (use browse_card.filter or browse_card [filter])."
                    )
                )

            link.FromNode.Substring(0, idx), link.FromNode.Substring(idx + 1)

    let private applyHostLink (cards: ResizeArray<CardDefinition>) (link: FlowLinkDef) =
        match tryParseHostConsumer link.ToNode with
        | None -> ()
        | Some consumerId ->
            let hostCardId, filterName = hostFilterFromLink link
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
                            $"Card '{consumerId}': host route conflicts with filters host '{existing}' (link expects '{hostCardId}')."
                        )
                    )
                | _ -> ()

                let hosted =
                    match consumer.HostedFilters with
                    | Some list -> ResizeArray(list)
                    | None -> ResizeArray()

                if not (hosted |> Seq.exists (fun f -> String.Equals(f, filterName, StringComparison.OrdinalIgnoreCase))) then
                    hosted.Add filterName

                cards.[index] <-
                    { consumer with
                        FilterHostCardId = Some hostCardId
                        HostedFilters = Some(hosted :> IReadOnlyList<_>) }

    let private applyLinks (cards: ResizeArray<CardDefinition>) (links: IReadOnlyList<FlowLinkDef>) =
        for link in links do
            applyHostLink cards link

    let apply (shell: DashboardShellContext) =
        match shell.ReportScopeFlow with
        | Some flow -> applyLinks shell.Cards flow.Links
        | None -> ()

        for page in shell.Pages do
            match page.ScopeFlow with
            | Some flow -> applyLinks shell.Cards flow.Links
            | None -> ()
