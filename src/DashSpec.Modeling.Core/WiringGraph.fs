namespace DashSpec.Modeling.Core

open System
open System.Collections.Generic

/// <summary>Unified document wiring IR (ADR-0091 / ADR-0092).</summary>
[<RequireQualifiedAccess>]
type WiringNodeKind =
    | ModuleNode
    | Filter
    | Slot
    | Chrome
    | Card
    | Phase
    | Page

[<RequireQualifiedAccess>]
type WiringEdgeKind =
    | Flow
    | Route
    | Event

type WiringNode =
    { Id: string
      Kind: WiringNodeKind
      Scope: string option }

type WiringEdge =
    { From: string
      FromPort: string option
      To: string
      ToPort: string option
      Kind: WiringEdgeKind
      Scope: string option }

type WiringGraph =
    { Nodes: IReadOnlyDictionary<string, WiringNode>
      Edges: WiringEdge[] }

module WiringGraph =

    let empty =
        { Nodes = Map.empty :> IReadOnlyDictionary<_, _>
          Edges = Array.empty }

    let private nodeKey (node: WiringNode) = node.Id

    let mergeNodes (nodes: seq<WiringNode>) =
        let map = Dictionary<string, WiringNode>(StringComparer.OrdinalIgnoreCase)
        for node in nodes do
            if String.IsNullOrWhiteSpace node.Id then
                invalidArg "nodes" "Wiring node id is required."
            map.[node.Id] <- node
        map :> IReadOnlyDictionary<_, _>

    let create (nodes: seq<WiringNode>) (edges: seq<WiringEdge>) =
        { Nodes = mergeNodes nodes
          Edges = edges |> Seq.toArray }
