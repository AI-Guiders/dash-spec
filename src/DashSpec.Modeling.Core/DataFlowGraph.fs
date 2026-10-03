namespace DashSpec.Modeling.Core

open System
open System.Collections.Generic

/// <summary>Dashflow data plane IR (ADR-0078).</summary>
[<RequireQualifiedAccess>]
type FlowNodeKind =
    | Source
    | Transformer
    | ApplyFilters

type FlowPort =
    { Name: string
      Type: DashPortType }

type FlowNode =
    { Id: string
      Kind: FlowNodeKind
      Inputs: FlowPort[]
      Outputs: FlowPort[] }

/// <summary>Endpoint on the <see cref="FlowGraph"/> — a node id and a port name (edge From/To), not an external SQL name.</summary>
type FlowNodePortRef =
    { NodeId: string
      PortName: string }

module FlowNodePortRef =

    let producer nodeId portName =
        if String.IsNullOrWhiteSpace nodeId then
            invalidArg "nodeId" "Flow producer node id is required."

        if String.IsNullOrWhiteSpace portName then
            invalidArg "portName" "Flow producer port name is required."

        { NodeId = nodeId; PortName = portName }

    let dotted (ref: FlowNodePortRef) = $"{ref.NodeId}.{ref.PortName}"

type FlowEdge =
    { From: FlowNodePortRef
      To: FlowNodePortRef }

type FlowGraph =
    { Nodes: IReadOnlyDictionary<string, FlowNode>
      Edges: FlowEdge[] }

type FlowGraphDiagnostic =
    { Code: string
      Message: string
      Edge: FlowEdge option }

module FlowGraph =

    let empty =
        { Nodes = Map.empty :> IReadOnlyDictionary<_, _>
          Edges = Array.empty }

    let ofNodes (nodes: seq<FlowNode>) (edges: seq<FlowEdge>) =
        let map = Dictionary<string, FlowNode>(StringComparer.OrdinalIgnoreCase)

        for node in nodes do
            if String.IsNullOrWhiteSpace node.Id then
                invalidArg "nodes" "Flow node id is required."

            if map.ContainsKey node.Id then
                raise (DashSpecParseException($"Duplicate flow node '{node.Id}'."))

            map.[node.Id] <- node

        { Nodes = map :> IReadOnlyDictionary<_, _>
          Edges = edges |> Seq.toArray }

    let private tryFindPort (node: FlowNode) (portName: string) (ports: FlowPort[]) =
        ports |> Array.tryFind (fun port -> String.Equals(port.Name, portName, StringComparison.OrdinalIgnoreCase))

    let private tryResolveOutput (graph: FlowGraph) (ref: FlowNodePortRef) =
        match graph.Nodes.TryGetValue ref.NodeId with
        | false, _ -> None
        | true, node -> tryFindPort node ref.PortName node.Outputs

    let private tryResolveInput (graph: FlowGraph) (ref: FlowNodePortRef) =
        match graph.Nodes.TryGetValue ref.NodeId with
        | false, _ -> None
        | true, node -> tryFindPort node ref.PortName node.Inputs

    /// <summary>Prove row-type compatibility on every edge (ADR-0079 compile-time graph check).</summary>
    let typeCheck (graph: FlowGraph) (catalog: TypeCatalog) : FlowGraphDiagnostic list =
        let diagnostics = ResizeArray<FlowGraphDiagnostic>()

        for edge in graph.Edges do
            match graph.Nodes.TryGetValue edge.From.NodeId, graph.Nodes.TryGetValue edge.To.NodeId with
            | (false, _), _
            | _, (false, _) ->
                diagnostics.Add
                    { Code = "DFLOW001"
                      Message = "Edge references unknown flow node."
                      Edge = Some edge }
            | (true, fromNode), (true, toNode) ->
                match tryFindPort fromNode edge.From.PortName fromNode.Outputs, tryFindPort toNode edge.To.PortName toNode.Inputs with
                | None, _
                | _, None ->
                    diagnostics.Add
                        { Code = "DFLOW002"
                          Message = $"Unknown port on edge {edge.From.NodeId}.{edge.From.PortName} → {edge.To.NodeId}.{edge.To.PortName}."
                          Edge = Some edge }
                | Some outputPort, Some inputPort ->
                    if not (TypeCatalog.portTypesCompatible outputPort.Type inputPort.Type) then
                        let describe (portType: DashPortType) =
                            match portType with
                            | DashPortType.Rows name -> $"rows {name}"

                        diagnostics.Add
                            { Code = "DFLOW003"
                              Message =
                                $"Port type mismatch: {describe outputPort.Type} → {describe inputPort.Type} on edge {edge.From.NodeId}.{edge.From.PortName} → {edge.To.NodeId}.{edge.To.PortName}."
                              Edge = Some edge }

                    match outputPort.Type with
                    | DashPortType.Rows rowName ->
                        match TypeCatalog.tryGet catalog rowName with
                        | None ->
                            diagnostics.Add
                                { Code = "DFLOW004"
                                  Message = $"Row type '{rowName}' is not defined in the type catalog."
                                  Edge = Some edge }
                        | Some _ -> ()

        diagnostics |> Seq.toList
