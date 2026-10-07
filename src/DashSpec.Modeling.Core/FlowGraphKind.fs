namespace DashSpec.Modeling.Core

open System

/// <summary>Qualified <c>{kind} flow</c> partition (ADR-0093). Extend via <see cref="FlowGraphKindRegistry"/>.</summary>
[<RequireQualifiedAccess>]
type FlowGraphKind =
    | Data
    | Show
    | Wire
    | Action

/// <summary>Single registry for flow kind keywords and block end labels — change kinds here only.</summary>
module FlowGraphKindRegistry =

    type FlowGraphKindDefinition =
        { Kind: FlowGraphKind
          Keyword: string
          BlockEndId: string }

    let private definitions: FlowGraphKindDefinition[] =
        [|
            { Kind = FlowGraphKind.Data; Keyword = "data"; BlockEndId = "flow" }
            { Kind = FlowGraphKind.Show; Keyword = "show"; BlockEndId = "flow" }
            { Kind = FlowGraphKind.Wire; Keyword = "wire"; BlockEndId = "flow" }
            { Kind = FlowGraphKind.Action; Keyword = "action"; BlockEndId = "flow" }
        |]

    let allDefinitions = definitions

    let allKinds = definitions |> Array.map (fun d -> d.Kind)

    let tryFindByKeyword (keyword: string) =
        definitions
        |> Array.tryFind (fun d -> String.Equals(d.Keyword, keyword, StringComparison.OrdinalIgnoreCase))
        |> Option.map (fun d -> d.Kind)

    let keyword (kind: FlowGraphKind) =
        definitions
        |> Array.find (fun d -> d.Kind = kind)
        |> fun d -> d.Keyword

    let blockEndId (kind: FlowGraphKind) =
        definitions
        |> Array.find (fun d -> d.Kind = kind)
        |> fun d -> d.BlockEndId

    /// <summary>Where a qualified flow block may appear (lint at parse time).</summary>
    [<RequireQualifiedAccess>]
    type FlowGraphScope =
        | ReportOrPage
        | CardInterior

    let allowedKinds (scope: FlowGraphScope) =
        match scope with
        | FlowGraphScope.ReportOrPage -> [| FlowGraphKind.Show; FlowGraphKind.Wire; FlowGraphKind.Action |]
        | FlowGraphScope.CardInterior -> allKinds

    let isAllowed (scope: FlowGraphScope) (kind: FlowGraphKind) =
        allowedKinds scope |> Array.exists ((=) kind)
