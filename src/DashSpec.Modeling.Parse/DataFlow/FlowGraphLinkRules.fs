namespace DashSpec.Modeling.Parse.DataFlow

open System
open System.Collections.Generic
open DashSpec.Modeling.Core

/// <summary>Infer link role for qualified-flow lint (ADR-0093 / ADR-0096).</summary>
module FlowGraphLinkRules =

    [<RequireQualifiedAccess>]
    type InferredLinkRole =
        | Data
        | Placement
        | Show
        | Wire
        | Action

    let private portEquals (link: FlowLinkDef) (name: string) =
        match link.ToPort with
        | Some port -> String.Equals(port, name, StringComparison.OrdinalIgnoreCase)
        | None -> false

    let private isChromeOrHostTarget (toNode: string) =
        let lower = toNode.ToLowerInvariant()
        lower.StartsWith("chrome.") || lower.StartsWith("host.")

    let inferRole (link: FlowLinkDef) : InferredLinkRole =
        let source = DashSpec.Modeling.Parse.Syntax.FlowEndpointSyntax.create link.FromNode link.FromPort
        let target = DashSpec.Modeling.Parse.Syntax.FlowEndpointSyntax.create link.ToNode link.ToPort

        let toPortEquals name =
            match target.Port with
            | Some(DashSpec.Modeling.Parse.Syntax.PortSelectorSyntax.Port p) ->
                String.Equals(p, name, StringComparison.OrdinalIgnoreCase)
            | _ -> false

        match target.Target with
        | DashSpec.Modeling.Parse.Syntax.FlowEndpointTarget.ReportAnchor
        | DashSpec.Modeling.Parse.Syntax.FlowEndpointTarget.PageAnchor _
        | DashSpec.Modeling.Parse.Syntax.FlowEndpointTarget.CardAnchor _ ->
            InferredLinkRole.Placement
        | DashSpec.Modeling.Parse.Syntax.FlowEndpointTarget.CardSlot _ ->
            InferredLinkRole.Data
        | DashSpec.Modeling.Parse.Syntax.FlowEndpointTarget.HostTarget _ ->
            InferredLinkRole.Wire
        | _ when toPortEquals "toolbar" || toPortEquals "panel" || toPortEquals "filters" ->
            InferredLinkRole.Show
        | DashSpec.Modeling.Parse.Syntax.FlowEndpointTarget.DashboardChrome
        | DashSpec.Modeling.Parse.Syntax.FlowEndpointTarget.SectionChrome _
        | DashSpec.Modeling.Parse.Syntax.FlowEndpointTarget.CardChrome _
            when not (toPortEquals "host") ->
            InferredLinkRole.Show
        | _ when
            source.Port
            |> Option.exists (fun p -> p.RawText.Contains("click", StringComparison.OrdinalIgnoreCase)) ->
            InferredLinkRole.Action
        | DashSpec.Modeling.Parse.Syntax.FlowEndpointTarget.PhaseTarget _ ->
            InferredLinkRole.Action
        | _ ->
            InferredLinkRole.Data

    let private roleName role =
        match role with
        | InferredLinkRole.Data -> "data"
        | InferredLinkRole.Placement -> "placement"
        | InferredLinkRole.Show -> "show"
        | InferredLinkRole.Wire -> "wire"
        | InferredLinkRole.Action -> "action"

    let private kindName (kind: FlowGraphKind) = FlowGraphKindRegistry.keyword kind

    let validateLink (contextLabel: string) (kind: FlowGraphKind) (link: FlowLinkDef) =
        let inferred = inferRole link

        let expected =
            match kind with
            | FlowGraphKind.Data -> InferredLinkRole.Data
            | FlowGraphKind.Placement -> InferredLinkRole.Placement
            | FlowGraphKind.Show -> InferredLinkRole.Show
            | FlowGraphKind.Wire -> InferredLinkRole.Wire
            | FlowGraphKind.Action -> InferredLinkRole.Action

        if inferred <> expected then
            raise (
                DashSpecScopeMismatchException(
                    $"{contextLabel}: link '{link.FromNode} -> {link.ToNode}' belongs in {roleName inferred} flow, not {kindName kind} flow."
                )
            )

    let validateLinks (contextLabel: string) (kind: FlowGraphKind) (links: IReadOnlyList<FlowLinkDef>) =
        for link in links do
            validateLink contextLabel kind link
