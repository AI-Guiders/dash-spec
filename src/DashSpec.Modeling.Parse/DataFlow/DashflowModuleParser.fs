namespace DashSpec.Modeling.Parse.DataFlow

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module DashflowModuleParser =

    let private readInlineAssignmentValue (reader: TokenReader) =
        reader.SkipNewlines()

        let first =
            match reader.CurrentKind with
            | TokenKind.String -> reader.ReadString()
            | TokenKind.Ident -> reader.ReadIdent()
            | _ -> raise (reader.Unexpected "assignment value")

        let sb = System.Text.StringBuilder(first)

        while not (reader.IsOnNewline()) && reader.RawKind = TokenKind.Slash do
            reader.Advance()
            sb.Append('/') |> ignore
            ignore (sb.Append(reader.ReadIdentSameLine()))

        sb.ToString()

    let private skipTransformStep (reader: TokenReader) =
        reader.ExpectKeyword "use"
        reader.ReadIdent() |> ignore
        reader.Expect TokenKind.LBrace

        while not (reader.IsAt TokenKind.RBrace) && not reader.IsEof do
            reader.SkipNewlines()

            if reader.IsAt TokenKind.RBrace then ()
            else
                reader.ReadIdent() |> ignore

                if reader.IsAt TokenKind.Eq then
                    reader.Advance()
                    readInlineAssignmentValue reader |> ignore

        reader.Expect TokenKind.RBrace

    let private applySourcePorts (ports: FlowPortsParser.ParsedPorts) (outputPort: byref<string>) (outputRowType: byref<string>) =
        if ports.Inputs.Length > 0 then
            raise (DashSpecParseException("source ports block cannot declare input ports."))

        match ports.Outputs with
        | [||] -> ()
        | [| decl |] ->
            if decl.Shape <> FlowPortsParser.PortShape.Stream then
                raise (DashSpecParseException("source output must be stream."))

            outputPort <- decl.Name
            outputRowType <- decl.ValueType
        | _ ->
            raise (DashSpecParseException("source requires exactly one output port in ports block (v1)."))

        ports.DefaultOutput

    let private parseSourceBlock (reader: TokenReader) (sourceId: string) =
        reader.Expect TokenKind.LBrace
        reader.SkipNewlines()
        let mutable providerBinding = None
        let mutable from: SourceFrom option = None
        let mutable outputPort = ""
        let mutable outputRowType = ""
        let mutable defaultOutputPort = None

        while not (reader.IsAt TokenKind.RBrace) && not reader.IsEof do
            reader.SkipNewlines()

            if reader.IsAt TokenKind.RBrace then ()
            elif reader.TryKeyword "use" then
                let useKind = reader.ReadIdent()

                if String.Equals(useKind, "connector", StringComparison.OrdinalIgnoreCase) then
                    raise (DashSpecParseException($"source '{sourceId}': use 'provider <id>', not 'connector' (connector is the runtime plugin; provider selects the manifest entry)."))

                if not (String.Equals(useKind, "provider", StringComparison.OrdinalIgnoreCase)) then
                    raise (DashSpecParseException($"source '{sourceId}' supports only 'use provider <id|infer>' before from/ports."))

                let providerName = reader.ReadIdent()

                if String.IsNullOrWhiteSpace providerName then
                    raise (DashSpecParseException($"source '{sourceId}': use provider requires an id or infer."))

                providerBinding <-
                    if String.Equals(providerName, "infer", StringComparison.OrdinalIgnoreCase) then
                        Some DashflowProviderBinding.Infer
                    else
                        Some(DashflowProviderBinding.Named providerName)
            elif reader.TryKeyword "ports" then
                let ports = FlowPortsParser.parsePortsBlock reader
                defaultOutputPort <- applySourcePorts ports &outputPort &outputRowType
            elif reader.TryKeyword "from" then
                if reader.TryKeyword "view" then
                    let viewName = AccessorGrammar.readDotted reader
                    from <- Some { Kind = SourceFromKind.View; Value = viewName }
                elif reader.TryKeyword "sql" then
                    if reader.TryKeyword "query" then
                        let body =
                            match reader.CurrentKind with
                            | TokenKind.String -> reader.ReadString()
                            | TokenKind.Raw -> reader.ReadRawBlock()
                            | _ -> raise (reader.Unexpected "sql query string")

                        from <- Some { Kind = SourceFromKind.SqlQuery; Value = body }
                    elif reader.TryKeyword "file" then
                        let path = reader.ReadString()
                        from <- Some { Kind = SourceFromKind.SqlFile; Value = path }
                    else
                        raise (DashSpecParseException("from sql requires query or file."))
                else
                    raise (DashSpecParseException("source from requires view or sql."))
            else
                raise (reader.Unexpected "from or ports")

            reader.SkipNewlines()

        reader.Expect TokenKind.RBrace

        match from with
        | None -> raise (DashSpecParseException($"source '{sourceId}' requires from view or from sql."))
        | Some value ->
            if String.IsNullOrWhiteSpace outputPort then
                raise (DashSpecParseException($"source '{sourceId}' requires a ports block with an output port."))

            match providerBinding with
            | None ->
                raise (
                    DashSpecParseException(
                        $"source '{sourceId}' requires use provider <id> or use provider infer (manifest default_provider_id; same as card datasource infer)."))
            | Some binding ->
                { Id = sourceId
                  Provider = binding
                  From = value
                  OutputPort = outputPort
                  OutputRowType = outputRowType
                  DefaultOutputPort = defaultOutputPort }

    let private parseTransformerBlock (reader: TokenReader) (transformerId: string) =
        reader.Expect TokenKind.LBrace
        reader.SkipNewlines()
        let inputs = ResizeArray<DashflowInputDecl>()
        let outputs = ResizeArray<string * string>()
        let mutable defaultInputPort = None
        let mutable defaultOutputPort = None

        while not (reader.IsAt TokenKind.RBrace) && not reader.IsEof do
            reader.SkipNewlines()

            if reader.IsAt TokenKind.RBrace then ()
            elif reader.TryKeyword "ports" then
                let ports = FlowPortsParser.parsePortsBlock reader
                defaultInputPort <- ports.DefaultInput
                defaultOutputPort <- ports.DefaultOutput

                for decl in ports.Inputs do
                    inputs.Add(
                        { Name = decl.Name
                          PortType = Some(FlowPortsParser.toDashPortType decl) })

                for decl in ports.Outputs do
                    outputs.Add((decl.Name, decl.ValueType))
            elif reader.TryKeyword "transform" then
                skipTransformStep reader
            else
                raise (reader.Unexpected "ports or transform")

            reader.SkipNewlines()

        reader.Expect TokenKind.RBrace

        { Id = transformerId
          Inputs = inputs.ToArray()
          Outputs = outputs.ToArray()
          DefaultInputPort = defaultInputPort
          DefaultOutputPort = defaultOutputPort }

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

    let private deriveCompositePorts (innerGraph: FlowGraph) =
        let hasInputWire (nodeId: string) (portName: string) =
            innerGraph.Edges
            |> Array.exists (fun edge ->
                String.Equals(edge.To.NodeId, nodeId, StringComparison.OrdinalIgnoreCase)
                && String.Equals(edge.To.PortName, portName, StringComparison.OrdinalIgnoreCase))

        let hasOutputWire (nodeId: string) (portName: string) =
            innerGraph.Edges
            |> Array.exists (fun edge ->
                String.Equals(edge.From.NodeId, nodeId, StringComparison.OrdinalIgnoreCase)
                && String.Equals(edge.From.PortName, portName, StringComparison.OrdinalIgnoreCase))

        let inputs =
            innerGraph.Nodes.Values
            |> Seq.collect (fun node ->
                node.Inputs
                |> Array.choose (fun port ->
                    if hasInputWire node.Id port.Name then None else Some port))
            |> Seq.distinctBy (fun port -> port.Name, port.Type)
            |> Seq.toArray

        let outputs =
            innerGraph.Nodes.Values
            |> Seq.collect (fun node ->
                node.Outputs
                |> Array.choose (fun port ->
                    if hasOutputWire node.Id port.Name then None else Some port))
            |> Seq.distinctBy (fun port -> port.Name, port.Type)
            |> Seq.toArray

        inputs, outputs

    let private compositePorts (nested: DashflowNestedFlowDef) =
        deriveCompositePorts nested.InnerGraph

    let private resolveCompositeConsumerPort (nested: DashflowNestedFlowDef) (port: string option) =
        let inputs, _ = compositePorts nested

        match port with
        | Some value -> value
        | None ->
            match inputs with
            | [| flowPort |] -> flowPort.Name
            | _ ->
                raise (
                    DashSpecParseException(
                        $"flow link to nested flow '{nested.Id}' requires an explicit input port in [] when multiple external inputs are exposed."
                    )
                )

    let private resolveCompositeProducerPort (nested: DashflowNestedFlowDef) (port: string option) =
        let _, outputs = compositePorts nested

        match port with
        | Some value -> value
        | None ->
            match outputs with
            | [| flowPort |] -> flowPort.Name
            | _ ->
                raise (
                    DashSpecParseException(
                        $"flow link from nested flow '{nested.Id}' requires an explicit output port in [] when multiple external outputs are exposed."
                    )
                )

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
            let inputs, outputs = compositePorts nested

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

    let private finishNestedFlow
        (flowId: string)
        (sources: DashflowSourceDef[])
        (transformers: DashflowTransformerDef[])
        (nestedFlows: DashflowNestedFlowDef[])
        (links: FlowLinkDef[])
        (typeCatalog: TypeCatalog)
        =
        let innerGraph = buildScopeGraph sources transformers nestedFlows links
        let diagnostics = FlowGraph.typeCheck innerGraph typeCatalog

        { Id = flowId
          Sources = sources
          Transformers = transformers
          NestedFlows = nestedFlows
          Links = links
          InnerGraph = innerGraph
          Diagnostics = diagnostics |> List.toArray }

    let rec private parseFlowBody
        (reader: TokenReader)
        (seenIds: HashSet<string>)
        (sources: ResizeArray<DashflowSourceDef>)
        (transformers: ResizeArray<DashflowTransformerDef>)
        (nestedFlows: ResizeArray<DashflowNestedFlowDef>)
        (links: ResizeArray<FlowLinkDef>)
        (typeCatalog: TypeCatalog)
        (isDone: unit -> bool)
        =
        while not (isDone ()) && not reader.IsEof do
            reader.SkipNewlines()

            if isDone () then ()
            elif reader.TryKeyword "source" then
                let sourceId = reader.ReadIdent()
                ensureUniqueNodeId "source" sourceId seenIds
                sources.Add(parseSourceBlock reader sourceId)
            elif reader.TryKeyword "transformer" then
                let transformerId = reader.ReadIdent()
                ensureUniqueNodeId "transformer" transformerId seenIds
                transformers.Add(parseTransformerBlock reader transformerId)
            elif reader.TryKeyword "flow" then
                nestedFlows.Add(parseNestedFlowBlock reader typeCatalog seenIds)
            else
                let saved = reader.SavePosition()
                let nodeId = reader.ReadIdent()
                let fromPort = FlowLinkParser.tryReadBracketPortSameLine reader

                match FlowLinkParser.tryParseLink reader nodeId fromPort with
                | Some link -> links.Add link
                | None ->
                    reader.RestorePosition saved
                    raise (reader.Unexpected "source, transformer, nested flow, or flow link")

    and private parseNestedFlowBlock (reader: TokenReader) (typeCatalog: TypeCatalog) (seenIds: HashSet<string>) =
        let flowId = reader.ReadIdent()
        ensureUniqueNodeId "nested flow" flowId seenIds
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let sources = ResizeArray<DashflowSourceDef>()
        let transformers = ResizeArray<DashflowTransformerDef>()
        let nestedFlows = ResizeArray<DashflowNestedFlowDef>()
        let links = ResizeArray<FlowLinkDef>()

        parseFlowBody
            reader
            seenIds
            sources
            transformers
            nestedFlows
            links
            typeCatalog
            (fun () -> BlockSyntax.isBlockEnd reader "flow" (Some flowId))

        BlockSyntax.expectBlockEnd reader "flow" (Some flowId)

        finishNestedFlow
            flowId
            (sources.ToArray())
            (transformers.ToArray())
            (nestedFlows.ToArray())
            (links.ToArray())
            typeCatalog

    let private finishModule
        (flowId: string)
        (sources: DashflowSourceDef[])
        (transformers: DashflowTransformerDef[])
        (nestedFlows: DashflowNestedFlowDef[])
        (links: FlowLinkDef[])
        (typeCatalog: TypeCatalog)
        =
        let graph = buildScopeGraph sources transformers nestedFlows links
        let diagnostics = FlowGraph.typeCheck graph typeCatalog

        { FlowId = flowId
          Sources = sources
          Transformers = transformers
          NestedFlows = nestedFlows
          Links = links
          Graph = graph
          Diagnostics = diagnostics |> List.toArray }

    /// Body after <c>@flow &lt;id&gt;</c> until <c>end flow</c> (optional matching id on <c>end</c>).
    let private parseFlowEnvelope (reader: TokenReader) (flowId: string) (typeCatalog: TypeCatalog) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let seenIds = HashSet<string>(StringComparer.OrdinalIgnoreCase)
        let sources = ResizeArray<DashflowSourceDef>()
        let transformers = ResizeArray<DashflowTransformerDef>()
        let nestedFlows = ResizeArray<DashflowNestedFlowDef>()
        let links = ResizeArray<FlowLinkDef>()

        parseFlowBody
            reader
            seenIds
            sources
            transformers
            nestedFlows
            links
            typeCatalog
            (fun () -> BlockSyntax.isBlockEnd reader "flow" (Some flowId))

        BlockSyntax.expectBlockEnd reader "flow" (Some flowId)

        finishModule
            flowId
            (sources.ToArray())
            (transformers.ToArray())
            (nestedFlows.ToArray())
            (links.ToArray())
            typeCatalog

    /// `.dashflow` fragment: <c>@flow &lt;id&gt;</c> … <c>end flow</c>.
    let parseModule (text: string) (typeCatalog: TypeCatalog) =
        if String.IsNullOrWhiteSpace text then invalidArg "text" "Dashflow text is required."
        let reader = ParserUtilities.createReader text
        reader.SkipFileDirectives()
        reader.SkipNewlines()

        if reader.TryKeyword "dataflow" then
            raise (DashSpecParseException("use '@flow <id>' … 'end flow', not 'dataflow'."))

        if reader.TryKeyword "flow" then
            raise (DashSpecParseException("dashflow modules start with '@flow <id>', not a bare 'flow' keyword."))

        reader.Expect TokenKind.At
        reader.ExpectKeyword "flow"
        let flowId = reader.ReadIdent()

        if String.IsNullOrWhiteSpace flowId then
            raise (DashSpecParseException("@flow requires an id."))

        reader.SkipNewlines()
        parseFlowEnvelope reader flowId typeCatalog
