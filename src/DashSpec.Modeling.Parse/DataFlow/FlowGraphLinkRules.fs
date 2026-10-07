namespace DashSpec.Modeling.Parse.DataFlow

open System
open System.Collections.Generic
open DashSpec.Modeling.Core

/// <summary>Infer link role for qualified-flow lint (ADR-0093).</summary>
module FlowGraphLinkRules =

    [<RequireQualifiedAccess>]
    type InferredLinkRole =
        | Data
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
        let toLower = link.ToNode.ToLowerInvariant()

        if toLower.StartsWith("card.") then
            InferredLinkRole.Data
        elif toLower.StartsWith("host.") then
            InferredLinkRole.Wire
        elif portEquals link "toolbar" || portEquals link "panel" || portEquals link "filters" then
            InferredLinkRole.Show
        elif isChromeOrHostTarget link.ToNode && portEquals link "host" |> not then
            InferredLinkRole.Show
        elif
            link.FromPort
            |> Option.exists (fun p -> p.Contains("click", StringComparison.OrdinalIgnoreCase))
        then
            InferredLinkRole.Action
        elif toLower.StartsWith("phase.") || toLower.StartsWith("page.") then
            InferredLinkRole.Action
        else
            InferredLinkRole.Data

    let private roleName role =
        match role with
        | InferredLinkRole.Data -> "data"
        | InferredLinkRole.Show -> "show"
        | InferredLinkRole.Wire -> "wire"
        | InferredLinkRole.Action -> "action"

    let private kindName (kind: FlowGraphKind) = FlowGraphKindRegistry.keyword kind

    let validateLink (contextLabel: string) (kind: FlowGraphKind) (link: FlowLinkDef) =
        let inferred = inferRole link

        let expected =
            match kind with
            | FlowGraphKind.Data -> InferredLinkRole.Data
            | FlowGraphKind.Show -> InferredLinkRole.Show
            | FlowGraphKind.Wire -> InferredLinkRole.Wire
            | FlowGraphKind.Action -> InferredLinkRole.Action

        if inferred <> expected then
            raise (
                DashSpecParseException(
                    $"{contextLabel}: link '{link.FromNode} -> {link.ToNode}' belongs in {roleName inferred} flow, not {kindName kind} flow."
                )
            )

    let validateLinks (contextLabel: string) (kind: FlowGraphKind) (links: IReadOnlyList<FlowLinkDef>) =
        for link in links do
            validateLink contextLabel kind link
