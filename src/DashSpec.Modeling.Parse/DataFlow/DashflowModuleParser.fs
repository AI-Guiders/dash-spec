namespace DashSpec.Modeling.Parse.DataFlow

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Syntax

module DashflowModuleParser =

    let private extractSourcePorts (ports: FlowPortsParser.ParsedPorts) : string * string * string option =
        if ports.Inputs.Length > 0 then
            raise (DashSpecParseException("source ports block cannot declare input ports."))

        let outputPort, outputRowType =
            match ports.Outputs with
            | [||] -> "", ""
            | [| decl |] ->
                if decl.Shape <> FlowPortsParser.PortShape.Stream then
                    raise (DashSpecParseException("source output must be stream."))
                decl.Name, decl.ValueType
            | _ ->
                raise (DashSpecParseException("source requires exactly one output port in ports block (v1)."))

        outputPort, outputRowType, ports.DefaultOutput

    type private ParsedOrigin =
        | PLeaf of kind: SourceFromKind * value: string * sheet: string option
        | PComposite of kind: CompositeOriginKind * left: string * right: string

    and private CompositeOriginKind =
        | JoinInner
        | JoinLeft
        | JoinRight
        | JoinFull
        | Union

    let private tryDefaultProducerPort
        (sources: DashflowSourceDef[])
        (transformers: DashflowTransformerDef[])
        (nodeId: string)
        =
        match sources |> Array.tryFind (fun s -> String.Equals(s.Id, nodeId, StringComparison.OrdinalIgnoreCase)) with
        | Some source ->
            match source.DefaultOutputPort with
            | Some name -> Some name
            | None -> Some source.OutputPort
        | None ->
            match transformers |> Array.tryFind (fun t -> String.Equals(t.Id, nodeId, StringComparison.OrdinalIgnoreCase)) with
            | None -> None
            | Some transformer ->
                match transformer.DefaultOutputPort with
                | Some name -> Some name
                | None ->
                    match transformer.Outputs with
                    | [| (name, _) |] -> Some name
                    | _ -> None

    let private resolveProducerPort
        (sources: DashflowSourceDef[])
        (transformers: DashflowTransformerDef[])
        (nodeId: string)
        (port: string option)
        =
        match port with
        | Some value -> value
        | None ->
            match tryDefaultProducerPort sources transformers nodeId with
            | Some name -> name
            | None ->
                raise (
                    DashSpecParseException(
                        $"flow link from '{nodeId}' requires an explicit output port when the node has multiple outputs."
                    )
                )

    let private resolveConsumerPort (transformer: DashflowTransformerDef) (port: string option) =
        match port with
        | Some value -> value
        | None ->
            match transformer.DefaultInputPort with
            | Some name -> name
            | None ->
                match transformer.Inputs with
                | [| input |] -> input.Name
                | _ ->
                    raise (
                        DashSpecParseException(
                            $"flow link to '{transformer.Id}' requires an explicit input port name when the transformer has multiple inputs and no default input is declared."
                        )
                    )

    let private tryOutputType (nodes: IReadOnlyDictionary<string, FlowNode>) (ref: FlowNodePortRef) =
        match nodes.TryGetValue ref.NodeId with
        | false, _ -> None
        | true, node ->
            node.Outputs
            |> Array.tryFind (fun port -> String.Equals(port.Name, ref.PortName, StringComparison.OrdinalIgnoreCase))
            |> Option.map (fun port -> port.Type)

    let private portTypeOnInnerNode (innerGraph: FlowGraph) (nodeId: string) (portName: string) (inputs: bool) =
        match innerGraph.Nodes.TryGetValue nodeId with
        | false, _ ->
            raise (DashSpecParseException($"nested flow ports: unknown inner node '{nodeId}'."))
        | true, node ->
            let ports = if inputs then node.Inputs else node.Outputs

            match ports |> Array.tryFind (fun p -> String.Equals(p.Name, portName, StringComparison.OrdinalIgnoreCase)) with
            | None ->
                raise (DashSpecParseException($"nested flow ports: node '{nodeId}' has no port '{portName}'."))
            | Some port -> port.Type

    let private compositePortsFromWires (nested: DashflowNestedFlowDef) =
        let inputs =
            nested.ExternalInputs
            |> Array.map (fun wire ->
                { Name = wire.ExternalName
                  Type = portTypeOnInnerNode nested.InnerGraph wire.InnerNodeId wire.InnerPortName true })

        let outputs =
            nested.ExternalOutputs
            |> Array.map (fun wire ->
                { Name = wire.ExternalName
                  Type = portTypeOnInnerNode nested.InnerGraph wire.InnerNodeId wire.InnerPortName false })

        inputs, outputs

    let private hasInnerIncoming (innerGraph: FlowGraph) (nodeId: string) =
        innerGraph.Edges
        |> Array.exists (fun edge -> String.Equals(edge.To.NodeId, nodeId, StringComparison.OrdinalIgnoreCase))

    let private hasInnerOutgoing (innerGraph: FlowGraph) (nodeId: string) =
        innerGraph.Edges
        |> Array.exists (fun edge -> String.Equals(edge.From.NodeId, nodeId, StringComparison.OrdinalIgnoreCase))

    let private hasInnerInputWire (innerGraph: FlowGraph) (nodeId: string) (portName: string) =
        innerGraph.Edges
        |> Array.exists (fun edge ->
            String.Equals(edge.To.NodeId, nodeId, StringComparison.OrdinalIgnoreCase)
            && String.Equals(edge.To.PortName, portName, StringComparison.OrdinalIgnoreCase))

    /// Composite <see cref="FlowPort.Name"/> must be unique; use <c>node.port</c> when the same inner port name appears on multiple boundary nodes.
    let private assignBoundaryExternalNames (wires: DashflowCompositePortWire[]) =
        let portNameCounts =
            wires
            |> Array.countBy (fun wire -> wire.InnerPortName.ToLowerInvariant())
            |> Map.ofArray

        wires
        |> Array.map (fun wire ->
            let needsQualified =
                match Map.tryFind (wire.InnerPortName.ToLowerInvariant()) portNameCounts with
                | Some count -> count > 1
                | None -> false

            let externalName =
                if needsQualified then $"{wire.InnerNodeId}.{wire.InnerPortName}" else wire.InnerPortName

            { wire with ExternalName = externalName })

    let private tryResolveBoundaryPortRef
        (flowId: string)
        (role: string)
        (wires: DashflowCompositePortWire[])
        (portRef: string)
        =
        match wires |> Array.tryFind (fun wire -> String.Equals(wire.ExternalName, portRef, StringComparison.OrdinalIgnoreCase)) with
        | Some wire -> wire.ExternalName
        | None ->
            let dot = portRef.IndexOf('.')

            if dot > 0 then
                let nodeId = portRef.Substring(0, dot)
                let portName = portRef.Substring(dot + 1)

                match
                    wires
                    |> Array.tryFind (fun wire ->
                        String.Equals(wire.InnerNodeId, nodeId, StringComparison.OrdinalIgnoreCase)
                        && String.Equals(wire.InnerPortName, portName, StringComparison.OrdinalIgnoreCase))
                with
                | Some wire -> wire.ExternalName
                | None ->
                    raise (DashSpecParseException($"no {role} port '{portRef}' on nested flow '{flowId}'."))
            else
                let matches =
                    wires
                    |> Array.filter (fun wire -> String.Equals(wire.InnerPortName, portRef, StringComparison.OrdinalIgnoreCase))

                match matches with
                | [| wire |] -> wire.ExternalName
                | [||] -> raise (DashSpecParseException($"no {role} port '{portRef}' on nested flow '{flowId}'."))
                | _ ->
                    raise (
                        DashSpecParseException(
                            $"ambiguous {role} port '{portRef}' on nested flow '{flowId}'; use [node.port] (e.g. [{matches.[0].InnerNodeId}.{portRef}])."
                        ))

    let private resolveCompositeConsumerPort (nested: DashflowNestedFlowDef) (port: string option) =
        match port with
        | Some value -> tryResolveBoundaryPortRef nested.Id "input" nested.ExternalInputs value
        | None ->
            match nested.ExternalInputs with
            | [| wire |] -> wire.ExternalName
            | _ ->
                raise (
                    DashSpecParseException(
                        $"flow link to nested flow '{nested.Id}' requires an explicit input port in [] when multiple inputs are declared."
                    )
                )

    let private resolveCompositeProducerPort (nested: DashflowNestedFlowDef) (port: string option) =
        match port with
        | Some value -> tryResolveBoundaryPortRef nested.Id "output" nested.ExternalOutputs value
        | None ->
            match nested.ExternalOutputs with
            | [| wire |] -> wire.ExternalName
            | _ ->
                raise (
                    DashSpecParseException(
                        $"flow link from nested flow '{nested.Id}' requires an explicit output port in [] when multiple outputs are declared."
                    )
                )

    /// <summary>Composite boundary = union of unwired entry inputs and exit outputs (same port names as inside).</summary>
    let private deriveSubprocessBoundary
        (flowId: string)
        (innerGraph: FlowGraph)
        (sources: DashflowSourceDef[])
        (transformers: DashflowTransformerDef[])
        =
        let inputWires = ResizeArray<DashflowCompositePortWire>()

        for transformer in transformers do
            if not (hasInnerIncoming innerGraph transformer.Id) then
                match innerGraph.Nodes.TryGetValue transformer.Id with
                | false, _ -> ()
                | true, node ->
                    for inputPort in node.Inputs do
                        if not (hasInnerInputWire innerGraph transformer.Id inputPort.Name) then
                            inputWires.Add
                                { ExternalName = ""
                                  InnerNodeId = transformer.Id
                                  InnerPortName = inputPort.Name }

        let outputWires = ResizeArray<DashflowCompositePortWire>()

        for source in sources do
            if not (hasInnerOutgoing innerGraph source.Id) then
                match innerGraph.Nodes.TryGetValue source.Id with
                | false, _ -> ()
                | true, node ->
                    for outputPort in node.Outputs do
                        outputWires.Add
                            { ExternalName = ""
                              InnerNodeId = source.Id
                              InnerPortName = outputPort.Name }

        for transformer in transformers do
            if not (hasInnerOutgoing innerGraph transformer.Id) then
                match innerGraph.Nodes.TryGetValue transformer.Id with
                | false, _ -> ()
                | true, node ->
                    for outputPort in node.Outputs do
                        outputWires.Add
                            { ExternalName = ""
                              InnerNodeId = transformer.Id
                              InnerPortName = outputPort.Name }

        let externalInputs = inputWires.ToArray()
        let externalOutputs = outputWires.ToArray()

        if externalInputs.Length = 0 && externalOutputs.Length = 0 then
            raise (DashSpecParseException($"nested flow '{flowId}' has no subprocess boundary ports (no entry inputs or exit outputs)."))

        assignBoundaryExternalNames externalInputs, assignBoundaryExternalNames externalOutputs

    let private buildScopeGraph
        (sources: DashflowSourceDef[])
        (transformers: DashflowTransformerDef[])
        (nestedFlows: DashflowNestedFlowDef[])
        (links: FlowLinkDef[])
        =
        let nodeList = ResizeArray<FlowNode>()
        let edges = ResizeArray<FlowEdge>()

        let nestedById =
            let map = Dictionary<string, DashflowNestedFlowDef>(StringComparer.OrdinalIgnoreCase)

            for nested in nestedFlows do
                map.[nested.Id] <- nested

            map :> IReadOnlyDictionary<_, _>

        for source in sources do
            nodeList.Add
                { Id = source.Id
                  Kind = FlowNodeKind.Source
                  Inputs = Array.empty
                  Outputs =
                    [| { Name = source.OutputPort
                         Type = DashPortType.Stream source.OutputRowType } |]
                  InnerFlowId = None }

        for transformer in transformers do
            let outputPorts =
                transformer.Outputs
                |> Array.map (fun (name, rowType) -> { Name = name; Type = DashPortType.Stream rowType })

            nodeList.Add
                { Id = transformer.Id
                  Kind = FlowNodeKind.Transformer
                  Inputs =
                    transformer.Inputs
                    |> Array.map (fun input ->
                        let portType =
                            match input.PortType with
                            | Some value -> value
                            | None -> DashPortType.Stream ""

                        { Name = input.Name; Type = portType })
                  Outputs = outputPorts
                  InnerFlowId = None }

        for nested in nestedFlows do
            let inputs, outputs = compositePortsFromWires nested

            nodeList.Add
                { Id = nested.Id
                  Kind = FlowNodeKind.Composite
                  Inputs = inputs
                  Outputs = outputs
                  InnerFlowId = Some nested.Id }

        let tryFindComposite (nodeId: string) =
            match nestedById.TryGetValue nodeId with
            | true, nested -> Some nested
            | false, _ -> None

        for link in links do
            let fromPortName =
                match tryFindComposite link.FromNode with
                | Some nested -> resolveCompositeProducerPort nested link.FromPort
                | None -> resolveProducerPort sources transformers link.FromNode link.FromPort

            match transformers |> Array.tryFind (fun t -> String.Equals(t.Id, link.ToNode, StringComparison.OrdinalIgnoreCase)) with
            | Some transformer ->
                let toPortName = resolveConsumerPort transformer link.ToPort

                edges.Add
                    { From = FlowNodePortRef.producer link.FromNode fromPortName
                      To = { NodeId = transformer.Id; PortName = toPortName } }
            | None ->
                match tryFindComposite link.ToNode with
                | None ->
                    raise (
                        DashSpecParseException(
                            $"flow link target '{link.ToNode}' is not a transformer or nested flow in this scope."
                        ))
                | Some nested ->
                    let toPortName = resolveCompositeConsumerPort nested link.ToPort

                    edges.Add
                        { From = FlowNodePortRef.producer link.FromNode fromPortName
                          To = { NodeId = nested.Id; PortName = toPortName } }

        let nodeMap =
            let map = Dictionary<string, FlowNode>(StringComparer.OrdinalIgnoreCase)

            for node in nodeList do
                map.[node.Id] <- node

            map :> IReadOnlyDictionary<_, _>

        let patchedNodes =
            nodeList
            |> Seq.map (fun node ->
                if node.Kind <> FlowNodeKind.Transformer then
                    node
                else
                    let inputs =
                        node.Inputs
                        |> Array.map (fun inputPort ->
                            let edge =
                                edges
                                |> Seq.tryFind (fun edge ->
                                    String.Equals(edge.To.NodeId, node.Id, StringComparison.OrdinalIgnoreCase)
                                    && String.Equals(edge.To.PortName, inputPort.Name, StringComparison.OrdinalIgnoreCase))

                            match edge with
                            | None -> inputPort
                            | Some wire ->
                                match tryOutputType nodeMap wire.From with
                                | None -> inputPort
                                | Some outputType -> { inputPort with Type = outputType })

                    { node with Inputs = inputs })
            |> Seq.toArray

        FlowGraph.ofNodes patchedNodes edges

    let private ensureUniqueNodeId (scope: string) (id: string) (seen: HashSet<string>) =
        if String.IsNullOrWhiteSpace id then
            raise (DashSpecParseException($"{scope} requires an id."))

        if seen.Contains id then
            raise (DashSpecParseException($"Duplicate flow id '{id}' in {scope}."))

        seen.Add id |> ignore

    /// <summary>
    /// The layering invariant (ADR-0126): database steps are the head of the chain
    /// (they compile into the query), runtime steps are the tail (they work over the
    /// loaded batch). A Database step may never follow a Runtime one — neither inside
    /// one transformer's step sequence nor across a link between transformers.
    /// </summary>
    let private checkStepLayering (transformers: DashflowTransformerDef[]) (links: FlowLinkDef[]) =
        let locality (pluginId: string) = TransformDeclarations.localityOf pluginId

        for transformer in transformers do
            let mutable seenRuntime = false

            for step in transformer.Steps do
                match locality step.PluginId with
                | "runtime" -> seenRuntime <- true
                | "database" when seenRuntime ->
                    raise (
                        DashSpecParseException(
                            $"transformer '{transformer.Id}': '{step.PluginId}' is a database transform and must precede runtime transforms (to_zone, group, reduce…)."))
                | _ -> ()

        let hasDatabaseStep (transformer: DashflowTransformerDef) =
            transformer.Steps
            |> Array.exists (fun step -> locality step.PluginId = "database")

        let hasRuntimeStep (transformer: DashflowTransformerDef) =
            transformer.Steps
            |> Array.exists (fun step -> locality step.PluginId = "runtime")

        let byId =
            transformers
            |> Array.map (fun t -> t.Id.ToUpperInvariant(), t)
            |> dict

        for link in links do
            match byId.TryGetValue(link.FromNode.ToUpperInvariant()),
                  byId.TryGetValue(link.ToNode.ToUpperInvariant()) with
            | (true, producer), (true, consumer) when hasRuntimeStep producer && hasDatabaseStep consumer ->
                raise (
                    DashSpecParseException(
                        $"flow link '{link.FromNode} -> {link.ToNode}': '{consumer.Id}' holds a database transform, "
                        + "which must run before runtime transforms — reorder the chain."))
            | _ -> ()

    /// <summary>ADR-0126 §1: composite-origin validation — both constituents
    /// must exist in this flow scope and be Leaf sources (no nested
    /// composition, v1); provider bindings must resolve equal (Infer always
    /// matches; two Named must be equal ids) and time_zone strings must match
    /// exactly. The composite inherits the resolved provider and time_zone.</summary>
    let private validateCompositeOrigins (sources: DashflowSourceDef[]) =
        let byId = Dictionary<string, DashflowSourceDef>(StringComparer.OrdinalIgnoreCase)

        for s in sources do
            byId.[s.Id] <- s

        let resolveConstituent (compositeId: string) (constituentId: string) =
            match byId.TryGetValue constituentId with
            | false, _ ->
                raise (
                    DashSpecParseException(
                        $"source '{compositeId}': composite constituent '{constituentId}' does not reference an existing source in this flow."))
            | true, def ->
                match def.Origin with
                | SourceOrigin.Leaf _ -> def
                | _ ->
                    raise (
                        DashSpecParseException(
                            $"source '{compositeId}': composite constituent '{constituentId}' is itself composite — nested composition is not supported in v1."))

        let providerOf (def: DashflowSourceDef) =
            match def.Provider with
            | DashflowProviderBinding.Infer -> None
            | DashflowProviderBinding.Named id -> Some id

        let patch (compositeId: string) (leftId: string) (rightId: string) (source: DashflowSourceDef) =
            let left = resolveConstituent compositeId leftId
            let right = resolveConstituent compositeId rightId

            let mergedProvider =
                match providerOf left, providerOf right with
                | Some lp, Some rp when not (String.Equals(lp, rp, StringComparison.OrdinalIgnoreCase)) ->
                    raise (
                        DashSpecParseException(
                            $"source '{compositeId}': composite constituents '{leftId}' (provider '{lp}') and '{rightId}' (provider '{rp}') resolve to different providers — cross-db composition is not supported."))
                | Some lp, _ | _, Some lp -> DashflowProviderBinding.Named lp
                | None, None -> DashflowProviderBinding.Infer

            let mergedTimeZone =
                if not (String.Equals(left.TimeZone, right.TimeZone, StringComparison.Ordinal)) then
                    raise (
                        DashSpecParseException(
                            $"source '{compositeId}': composite constituents '{leftId}' (time_zone '{left.TimeZone}') and '{rightId}' (time_zone '{right.TimeZone}') disagree — both must declare the same zone or none."))
                else
                    left.TimeZone

            { source with
                Provider = mergedProvider
                TimeZone = mergedTimeZone }

        sources
        |> Array.map (fun source ->
            match source.Origin with
            | SourceOrigin.Leaf _ -> source
            | SourceOrigin.InnerJoin(l, r, _) -> patch source.Id l r source
            | SourceOrigin.LeftJoin(l, r, _) -> patch source.Id l r source
            | SourceOrigin.RightJoin(l, r, _) -> patch source.Id l r source
            | SourceOrigin.FullJoin(l, r, _) -> patch source.Id l r source
            | SourceOrigin.UnionAll(l, r) -> patch source.Id l r source)

    let private finishNestedFlow
        (flowId: string)
        (sources: DashflowSourceDef[])
        (transformers: DashflowTransformerDef[])
        (nestedFlows: DashflowNestedFlowDef[])
        (links: FlowLinkDef[])
        (typeCatalog: TypeCatalog)
        =
        let innerGraph = buildScopeGraph sources transformers nestedFlows links
        checkStepLayering transformers links
        let diagnostics = FlowGraph.typeCheck innerGraph typeCatalog
        let sources = validateCompositeOrigins sources

        let externalInputs, externalOutputs =
            deriveSubprocessBoundary flowId innerGraph sources transformers

        { Id = flowId
          ExternalInputs = externalInputs
          ExternalOutputs = externalOutputs
          Sources = sources
          Transformers = transformers
          NestedFlows = nestedFlows
          Links = links
          InnerGraph = innerGraph
          Diagnostics = diagnostics |> List.toArray }

    /// <summary>ADR-0127 §4: a ports-less transformer gets the default unary
    /// ports — input <c>raw</c>, output <c>rows</c> — with the output type left
    /// empty for inference to fill.</summary>
    let private applyDefaultPorts (transformers: DashflowTransformerDef[]) =
        transformers
        |> Array.map (fun transformer ->
            if transformer.Inputs.Length > 0 || transformer.Outputs.Length > 0 then
                transformer
            else
                { transformer with
                    Inputs = [| { Name = "raw"; PortType = None } |]
                    Outputs = [| ("rows", "") |]
                    DefaultInputPort = Some "raw"
                    DefaultOutputPort = Some "rows" })

    /// <summary>ADR-0127: run port type inference, patch untyped outputs with
    /// inferred names, and typecheck against the catalog extended with the
    /// synthesized types.</summary>
    let private inferAndCheck
        (sources: DashflowSourceDef[])
        (transformers: DashflowTransformerDef[])
        (nestedFlows: DashflowNestedFlowDef[])
        (links: FlowLinkDef[])
        (typeCatalog: TypeCatalog)
        =
        let mergeIntoCatalog (baseCatalog: TypeCatalog) (synthesized: RowTypeDef list) =
            if synthesized.IsEmpty then
                baseCatalog
            else
                let synthesizedNames =
                    synthesized
                    |> List.map (fun d -> d.Name)
                    |> fun names -> System.Collections.Generic.HashSet<string>(names, StringComparer.OrdinalIgnoreCase)

                TypeCatalog.ofDefinitions (
                    seq {
                        for existing in baseCatalog.Definitions.Values do
                            if not (synthesizedNames.Contains existing.Name) then
                                yield existing

                        yield! synthesized
                    }
                )

        let sources, compositeSynthesized, _compositeDiagnostics =
            TypeInference.resolveCompositeSources sources typeCatalog

        let compositeCatalog = mergeIntoCatalog typeCatalog compositeSynthesized

        let outcome = TypeInference.inferScope sources transformers links compositeCatalog
        let allSynthesized = compositeSynthesized @ outcome.Synthesized

        let patchedTransformers =
            transformers
            |> Array.map (fun transformer ->
                let outputs =
                    transformer.Outputs
                    |> Array.map (fun (name, declared) ->
                        if not (String.IsNullOrWhiteSpace declared) then
                            (name, declared)
                        else
                            match outcome.OutputTypes.TryGetValue((transformer.Id, name)) with
                            | true, inferred -> (name, inferred)
                            | false, _ -> (name, declared))

                { transformer with Outputs = outputs })

        let extendedCatalog = mergeIntoCatalog compositeCatalog outcome.Synthesized

        let patchedGraph = buildScopeGraph sources patchedTransformers nestedFlows links
        let diagnostics = FlowGraph.typeCheck patchedGraph extendedCatalog

        let inferenceDiagnostics =
            outcome.Diagnostics
            |> List.map (fun d ->
                { Code = d.Code
                  Message = d.Message
                  Edge = None })

        patchedGraph, sources, patchedTransformers, allSynthesized, inferenceDiagnostics @ diagnostics

    let private finishModule
        (flowId: string)
        (sources: DashflowSourceDef[])
        (transformers: DashflowTransformerDef[])
        (nestedFlows: DashflowNestedFlowDef[])
        (links: FlowLinkDef[])
        (typeCatalog: TypeCatalog)
        =
        let transformers = applyDefaultPorts transformers
        checkStepLayering transformers links
        let sources = validateCompositeOrigins sources
        let graph, sources, transformers, inferred, diagnostics = inferAndCheck sources transformers nestedFlows links typeCatalog

        { FlowId = flowId
          Sources = sources
          Transformers = transformers
          NestedFlows = nestedFlows
          Links = links
          Graph = graph
          InferredRowTypes = inferred |> List.toArray
          EmitViews = false
          Diagnostics = diagnostics |> List.toArray }

    // --- ADR-0130: pure AST projection for flow modules (zero TokenReader rebuild) ---

    let private mapLeafKind (k: SourceLeafKindSyntax) : SourceFromKind =
        match k with
        | SourceLeafKindSyntax.View -> SourceFromKind.View
        | SourceLeafKindSyntax.SqlQuery -> SourceFromKind.SqlQuery
        | SourceLeafKindSyntax.SqlFile -> SourceFromKind.SqlFile
        | SourceLeafKindSyntax.Xlsx -> SourceFromKind.Xlsx

    let private mapCompositeKind (k: CompositeOriginKindSyntax) : CompositeOriginKind =
        match k with
        | CompositeOriginKindSyntax.InnerJoin -> JoinInner
        | CompositeOriginKindSyntax.LeftJoin -> JoinLeft
        | CompositeOriginKindSyntax.RightJoin -> JoinRight
        | CompositeOriginKindSyntax.FullJoin -> JoinFull
        | CompositeOriginKindSyntax.UnionAll -> Union

    let private mapOriginSyntax (origin: SourceOriginSyntax) : ParsedOrigin =
        match origin with
        | SourceOriginSyntax.Leaf(k, target, sheet) -> PLeaf(mapLeafKind k, target, sheet)
        | SourceOriginSyntax.Composite(k, left, right) -> PComposite(mapCompositeKind k, left, right)

    let private readIdentListFromProperty (sourceId: string) (prop: PropertySyntax) : string list =
        let items = ResizeArray<string>()
        let mutable idx = 0

        while idx < prop.ValueTokens.Length do
            if prop.ValueTokens.[idx].LexKind = TokenKind.Ident then
                items.Add prop.ValueTokens.[idx].Text
                idx <- idx + 1
                if idx < prop.ValueTokens.Length then
                    if prop.ValueTokens.[idx].LexKind = TokenKind.Comma then
                        idx <- idx + 1
                    else
                        raise (DashSpecParseException($"source '{sourceId}': expected ',' in '{prop.Key}' column list."))
            else
                raise (DashSpecParseException($"source '{sourceId}': expected identifier in '{prop.Key}' column list."))

        if items.Count = 0 then
            raise (DashSpecParseException($"source '{sourceId}': '{prop.Key}' requires at least one column identifier."))

        List.ofSeq items

    let private projectSourceFromAst (sourceId: string) (block: BlockDeclarationSyntax) : DashflowSourceDef =
        let annotationRowType = block.TypeAnnotation
        let headerOrigin = block.Origin |> Option.map mapOriginSyntax

        let hasBraceOnHeader =
            block.Tokens |> Array.exists (fun t -> t.LexKind = TokenKind.LBrace)

        if not hasBraceOnHeader then
            match headerOrigin with
            | Some(PComposite _) when block.Members.Length = 0 -> ()
            | _ -> raise (DashSpecParseException($"source '{sourceId}': expected '{{' after source header."))

        let mutable providerBinding: DashflowProviderBinding option = None
        let mutable from: SourceFrom option = None
        let mutable onKeys: string list = []
        let mutable leftKeys: string list = []
        let mutable rightKeys: string list = []
        let mutable timeZone = ""
        let mutable outputPort = ""
        let mutable outputRowType = ""
        let mutable defaultOutputPort = None

        for memberNode in block.Members do
            match memberNode with
            | DashSpecAstNode.PropertyAssignment prop ->
                if String.Equals(prop.Key, "time_zone", StringComparison.OrdinalIgnoreCase) then
                    let raw =
                        if prop.ValueTokens.Length = 1 && prop.ValueTokens.[0].LexKind = TokenKind.String then
                            let s = prop.ValueTokens.[0].Text
                            if s.Length >= 2 && s.[0] = '"' && s.[s.Length - 1] = '"' then s.Substring(1, s.Length - 2) else s
                        else
                            prop.ValueText

                    let value = raw.Trim().ToLowerInvariant()
                    if value <> "utc" && value <> "local" then
                        raise (DashSpecParseException($"source '{sourceId}': time_zone supports utc or local (got '{raw}')."))
                    timeZone <- value
                elif String.Equals(prop.Key, "on", StringComparison.OrdinalIgnoreCase) then
                    onKeys <- readIdentListFromProperty sourceId prop
                elif String.Equals(prop.Key, "left_key", StringComparison.OrdinalIgnoreCase) then
                    leftKeys <- readIdentListFromProperty sourceId prop
                elif String.Equals(prop.Key, "right_key", StringComparison.OrdinalIgnoreCase) then
                    rightKeys <- readIdentListFromProperty sourceId prop
                else
                    raise (DashSpecParseException($"source '{sourceId}': unknown property '{prop.Key}'."))
            | DashSpecAstNode.ClauseDeclaration clause ->
                match clause.Kind with
                | ClauseKind.UseProviderClause providerSyntax ->
                    match headerOrigin with
                    | Some(PComposite _) ->
                        raise (DashSpecParseException($"source '{sourceId}': a composite source inherits use provider from its constituents — declare it on them, not here."))
                    | _ -> ()

                    providerBinding <-
                        match providerSyntax with
                        | ProviderBindingSyntax.Infer -> Some DashflowProviderBinding.Infer
                        | ProviderBindingSyntax.Named id -> Some(DashflowProviderBinding.Named id)
                | ClauseKind.UseClause(useKind, _) ->
                    if String.Equals(useKind, "connector", StringComparison.OrdinalIgnoreCase) then
                        raise (DashSpecParseException($"source '{sourceId}': use 'provider <id>', not 'connector' (connector is the runtime plugin; provider selects the manifest entry)."))
                    raise (DashSpecParseException($"source '{sourceId}' supports only 'use provider <id|infer>' before from/ports."))
                | ClauseKind.FromClause originSyntax ->
                    if headerOrigin.IsSome then
                        raise (DashSpecParseException($"source '{sourceId}': origin already declared in the header (`= …`) — 'from' cannot be declared twice."))

                    match originSyntax with
                    | SourceOriginSyntax.Leaf(k, value, sheet) ->
                        from <- Some { Kind = mapLeafKind k; Value = value; Sheet = sheet }
                    | _ ->
                        raise (DashSpecParseException($"source '{sourceId}': 'from' clause requires a leaf origin (view, sql, or xlsx)."))
                | _ ->
                    let lineText = clause.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                    raise (DashSpecParseException($"source '{sourceId}': unexpected clause '{lineText}'."))
            | DashSpecAstNode.BlockDeclaration childBlock ->
                match childBlock.Opener with
                | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Ports
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Ports, _) ->
                    match headerOrigin with
                    | Some(PComposite _) ->
                        raise (DashSpecParseException($"source '{sourceId}': a composite source has no ports block — annotate the row type in the header (`source {sourceId}: Type = …`) or leave it for synthesis."))
                    | _ -> ()

                    let ports = FlowPortsParser.projectPortsFromAst childBlock.Members
                    let port, rowType, defOut = extractSourcePorts ports
                    outputPort <- port
                    outputRowType <- rowType
                    defaultOutputPort <- defOut
                | _ ->
                    let headerText = childBlock.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                    raise (DashSpecParseException($"source '{sourceId}': unexpected block '{headerText}'."))
            | DashSpecAstNode.BlankLine _
            | DashSpecAstNode.EndBlock _ -> ()
            | memberNode when DashSpecAst.isTrivia memberNode -> ()
            | other ->
                let toks = DashSpecAst.tokens other
                if toks.Length >= 2 && String.Equals(toks.[0].Text, "from", StringComparison.OrdinalIgnoreCase) then
                    if String.Equals(toks.[1].Text, "sql", StringComparison.OrdinalIgnoreCase) then
                        raise (DashSpecParseException("from sql requires query or file."))
                    elif String.Equals(toks.[1].Text, "xlsx", StringComparison.OrdinalIgnoreCase) then
                        raise (DashSpecParseException("from xlsx requires file."))
                    else
                        raise (DashSpecParseException("source from requires view, sql, or xlsx."))
                let lineText = toks |> Array.map (fun t -> t.Text) |> String.concat " "
                raise (DashSpecParseException($"source '{sourceId}': unexpected statement '{lineText}'."))

        let origin: SourceOrigin option =
            match headerOrigin, from with
            | Some(PLeaf(kind, value, sheet)), None ->
                if not onKeys.IsEmpty || not leftKeys.IsEmpty || not rightKeys.IsEmpty then
                    raise (DashSpecParseException($"source '{sourceId}': `on` / `left_key` / `right_key` are only valid on composite sources (InnerJoin/LeftJoin/RightJoin/FullJoin/UnionAll)."))
                Some(SourceOrigin.Leaf(kind, value, sheet))
            | Some(PComposite(kind, left, right)), None ->
                let joinCond =
                    if not onKeys.IsEmpty then
                        if not leftKeys.IsEmpty || not rightKeys.IsEmpty then
                            raise (DashSpecParseException($"source '{sourceId}': `on` and `left_key`/`right_key` cannot be mixed — pick one join-condition form."))
                        Some { On = onKeys; LeftKey = []; RightKey = [] }
                    elif not leftKeys.IsEmpty || not rightKeys.IsEmpty then
                        if leftKeys.IsEmpty || rightKeys.IsEmpty then
                            raise (DashSpecParseException($"source '{sourceId}': join origin with `left_key` requires `right_key` too (and vice versa)."))
                        Some { On = []; LeftKey = leftKeys; RightKey = rightKeys }
                    else
                        None

                let join left right cond =
                    match kind with
                    | JoinInner -> SourceOrigin.InnerJoin(left, right, cond)
                    | JoinLeft -> SourceOrigin.LeftJoin(left, right, cond)
                    | JoinRight -> SourceOrigin.RightJoin(left, right, cond)
                    | JoinFull -> SourceOrigin.FullJoin(left, right, cond)
                    | Union -> SourceOrigin.UnionAll(left, right)

                match kind with
                | Union ->
                    if not onKeys.IsEmpty || not leftKeys.IsEmpty || not rightKeys.IsEmpty then
                        raise (DashSpecParseException($"source '{sourceId}': UnionAll takes no join condition (`on` / `left_key` / `right_key` are not allowed)."))
                    Some(SourceOrigin.UnionAll(left, right))
                | _ ->
                    match joinCond with
                    | None ->
                        raise (DashSpecParseException($"source '{sourceId}': join origin requires `on = col` or `left_key = …` + `right_key = …` in the body."))
                    | Some cond -> Some(join left right cond)
            | Some _, Some _ -> None
            | None, Some legacy ->
                if not onKeys.IsEmpty || not leftKeys.IsEmpty || not rightKeys.IsEmpty then
                    raise (DashSpecParseException($"source '{sourceId}': `on` / `left_key` / `right_key` are only valid on composite sources (InnerJoin/LeftJoin/RightJoin/FullJoin/UnionAll)."))
                Some(SourceOrigin.Leaf(legacy.Kind, legacy.Value, legacy.Sheet))
            | None, None ->
                raise (DashSpecParseException($"source '{sourceId}' requires an origin: `= View/SqlQuery/SqlFile/Xlsx/…` in the header or legacy `from view|sql|xlsx` in the body."))

        match origin with
        | None -> raise (DashSpecParseException($"source '{sourceId}' requires an origin."))
        | Some originValue ->
            let isComposite =
                match originValue with
                | SourceOrigin.Leaf _ -> false
                | _ -> true

            if not isComposite then
                match annotationRowType with
                | Some ann when
                    not (String.IsNullOrWhiteSpace outputRowType)
                    && not (String.Equals(ann, outputRowType, StringComparison.Ordinal)) ->
                    raise (DashSpecParseException($"source '{sourceId}': row type annotated in the header ('{ann}') differs from the ports block ('{outputRowType}') — declare one."))
                | _ -> ()

            let finalRowType =
                if isComposite then
                    defaultArg annotationRowType ""
                elif not (String.IsNullOrWhiteSpace outputRowType) then
                    outputRowType
                else
                    defaultArg annotationRowType ""

            let finalPort =
                if not (String.IsNullOrWhiteSpace outputPort) then
                    outputPort
                elif isComposite || headerOrigin.IsSome then
                    "rows"
                else
                    ""

            match providerBinding with
            | None when not isComposite ->
                raise (DashSpecParseException($"source '{sourceId}' requires use provider <id> or use provider infer (manifest default_provider_id; same as card datasource infer)."))
            | _ -> ()

            if not isComposite && String.IsNullOrWhiteSpace finalPort then
                raise (DashSpecParseException($"source '{sourceId}' requires a ports block with an output port."))

            { Id = sourceId
              Provider = match providerBinding with Some b -> b | None -> DashflowProviderBinding.Infer
              Origin = originValue
              TimeZone = timeZone
              OutputPort = finalPort
              OutputRowType = finalRowType
              DefaultOutputPort = defaultOutputPort }

    let private projectTransformerFromAst (transformerId: string) (block: BlockDeclarationSyntax) : DashflowTransformerDef =
        let inputs = ResizeArray<DashflowInputDecl>()
        let outputs = ResizeArray<string * string>()
        let paramsDecls = ResizeArray<DashflowParamPortDef>()
        let steps = ResizeArray<DashflowTransformStepDef>()
        let mutable defaultInputPort = None
        let mutable defaultOutputPort = None

        for memberNode in block.Members do
            match memberNode with
            | DashSpecAstNode.BlockDeclaration childBlock ->
                match childBlock.Opener with
                | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Ports
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Ports, _) ->
                    let ports = FlowPortsParser.projectPortsFromAst childBlock.Members
                    defaultInputPort <- ports.DefaultInput
                    defaultOutputPort <- ports.DefaultOutput

                    for decl in ports.Inputs do
                        inputs.Add
                            { Name = decl.Name
                              PortType = Some(FlowPortsParser.toDashPortType decl) }

                    for decl in ports.Outputs do
                        outputs.Add((decl.Name, decl.ValueType))

                    for decl in ports.Params do
                        paramsDecls.Add
                            { Name = decl.Name
                              ValueType = decl.ValueType
                              Default = decl.Default
                              DependsOn = None
                              ColumnBinding = decl.ColumnBinding }
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Transform, pluginId) ->
                    steps.Add(TransformStepParser.projectStepFromAst transformerId pluginId childBlock)
                | _ ->
                    let headerText = childBlock.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                    raise (DashSpecParseException($"transformer '{transformerId}': unexpected block '{headerText}'."))
            | DashSpecAstNode.BlankLine _
            | DashSpecAstNode.EndBlock _ -> ()
            | memberNode when DashSpecAst.isTrivia memberNode -> ()
            | other ->
                let lineText = DashSpecAst.tokens other |> Array.map (fun t -> t.Text) |> String.concat " "
                raise (DashSpecParseException($"transformer '{transformerId}': unexpected statement '{lineText}' (expected ports or transform)."))

        { Id = transformerId
          Inputs = inputs.ToArray()
          Outputs = outputs.ToArray()
          Params = paramsDecls.ToArray()
          DefaultInputPort = defaultInputPort
          DefaultOutputPort = defaultOutputPort
          Steps = steps.ToArray() }

    /// <summary>One flow scope from typed tree members: Source, Transformer, FlowLink,
    /// and nested Flow blocks project directly from typed AST nodes.</summary>
    let rec private projectScope
        (members: DashSpecAstNode[])
        (typeCatalog: TypeCatalog)
        (seenIds: HashSet<string>)
        : DashflowSourceDef[] * DashflowTransformerDef[] * DashflowNestedFlowDef[] * FlowLinkDef[]
        =
        let sources = ResizeArray<DashflowSourceDef>()
        let transformers = ResizeArray<DashflowTransformerDef>()
        let nestedFlows = ResizeArray<DashflowNestedFlowDef>()
        let links = ResizeArray<FlowLinkDef>()

        for memberNode in members do
            match memberNode with
            | DashSpecAstNode.BlockDeclaration block ->
                match block.Opener with
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Source, sourceId) ->
                    ensureUniqueNodeId "source" sourceId seenIds
                    sources.Add(projectSourceFromAst sourceId block)
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Transformer, transformerId) ->
                    ensureUniqueNodeId "transformer" transformerId seenIds
                    transformers.Add(projectTransformerFromAst transformerId block)
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Flow, nestedId) ->
                    ensureUniqueNodeId "nested flow" nestedId seenIds
                    let s, t, n, l = projectScope block.Members typeCatalog seenIds
                    nestedFlows.Add(finishNestedFlow nestedId s t n l typeCatalog)
                | _ ->
                    let headerText = block.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                    raise (DashSpecParseException($"Unexpected block '{headerText}' in flow scope."))
            | DashSpecAstNode.FlowLinkDeclaration link ->
                links.Add
                    { FromNode = link.FromNode
                      FromPort = link.FromPort
                      ToNode = link.ToNode
                      ToPort = link.ToPort }
            | DashSpecAstNode.ClauseDeclaration clause ->
                match clause.Kind with
                | ClauseKind.NamespaceClause _ -> ()
                | _ ->
                    let lineText = clause.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                    raise (DashSpecParseException($"Unexpected clause '{lineText}' in flow scope."))
            | DashSpecAstNode.ImportDeclaration _
            | DashSpecAstNode.BlankLine _
            | DashSpecAstNode.EndBlock _ -> ()
            | memberNode when DashSpecAst.isTrivia memberNode -> ()
            | other ->
                let lineText = DashSpecAst.tokens other |> Array.map (fun t -> t.Text) |> String.concat " "
                raise (DashSpecParseException($"Unexpected statement '{lineText}' in flow scope (expected source, transformer, nested flow, or flow link)."))

        sources.ToArray(), transformers.ToArray(), nestedFlows.ToArray(), links.ToArray()

    let parseModule (text: string) (typeCatalog: TypeCatalog) =
        if String.IsNullOrWhiteSpace text then invalidArg "text" "Dashflow text is required."

        let tree = DashSpecSurfaceSyntax.parse text
        let env = UnitGrammar.unwrap (DashSpecModuleKind.Flow false) tree.Root

        let emitViews =
            match env.Kind with
            | DashSpecModuleKind.Flow v -> v
            | _ -> false

        let seenIds = HashSet<string>(StringComparer.OrdinalIgnoreCase)
        let sources, transformers, nestedFlows, links = projectScope env.Body typeCatalog seenIds
        let parsed = finishModule env.Id sources transformers nestedFlows links typeCatalog
        if emitViews then { parsed with EmitViews = true } else parsed
