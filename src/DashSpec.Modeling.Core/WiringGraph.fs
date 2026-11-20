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
    | Nest
    | Group

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

    /// <summary>
    /// Slice the <see cref="WiringGraph"/> to the <paramref name="maxHops"/>-hop neighborhood around <paramref name="focusId"/>.
    /// Returns the full graph unchanged if <paramref name="focusId"/> is empty or not found.
    /// </summary>
    let sliceNeighborhood (focusId: string) (maxHops: int) (graph: WiringGraph) : WiringGraph =
        if String.IsNullOrWhiteSpace focusId then
            graph
        else
            let needle = focusId.Trim()
            let centerOpt =
                graph.Nodes.Keys
                |> Seq.tryFind (fun k -> String.Equals(k, needle, StringComparison.OrdinalIgnoreCase))
                |> Option.orElseWith (fun () ->
                    graph.Nodes.Keys
                    |> Seq.tryFind (fun k -> k.Contains(needle, StringComparison.OrdinalIgnoreCase)))

            match centerOpt with
            | None -> graph
            | Some center ->
                let visited = HashSet<string>(StringComparer.OrdinalIgnoreCase)
                visited.Add center |> ignore
                let mutable frontier = [ center ]

                for _ in 1 .. (max 1 maxHops) do
                    let next = ResizeArray<string>()
                    for cur in frontier do
                        for edge in graph.Edges do
                            if String.Equals(edge.From, cur, StringComparison.OrdinalIgnoreCase) && visited.Add edge.To then
                                next.Add edge.To
                            if String.Equals(edge.To, cur, StringComparison.OrdinalIgnoreCase) && visited.Add edge.From then
                                next.Add edge.From
                    frontier <- List.ofSeq next

                let nodes =
                    graph.Nodes.Values
                    |> Seq.filter (fun n -> visited.Contains n.Id)

                let edges =
                    graph.Edges
                    |> Array.filter (fun e -> visited.Contains e.From && visited.Contains e.To)

                create nodes edges
