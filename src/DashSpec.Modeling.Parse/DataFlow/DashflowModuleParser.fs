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
        let mutable providerId = None
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
                    raise (DashSpecParseException($"source '{sourceId}' supports only 'use provider <id>' before from/ports."))

                providerId <- Some(reader.ReadIdent())
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

            { Id = sourceId
              ProviderId = providerId
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

    let private buildGraph
        (sources: DashflowSourceDef[])
        (transformers: DashflowTransformerDef[])
        (links: FlowLinkDef[])
        =
        let nodeList = ResizeArray<FlowNode>()
        let edges = ResizeArray<FlowEdge>()

        for source in sources do
            nodeList.Add
                { Id = source.Id
                  Kind = FlowNodeKind.Source
                  Inputs = Array.empty
                  Outputs =
                    [| { Name = source.OutputPort
                         Type = DashPortType.Stream source.OutputRowType } |] }

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
                  Outputs = outputPorts }

        for link in links do
            match transformers |> Array.tryFind (fun t -> String.Equals(t.Id, link.ToNode, StringComparison.OrdinalIgnoreCase)) with
            | None ->
                raise (DashSpecParseException($"flow link target '{link.ToNode}' is not a transformer in this flow block."))
            | Some transformer ->
                let fromPortName = resolveProducerPort sources transformers link.FromNode link.FromPort
                let toPortName = resolveConsumerPort transformer link.ToPort

                edges.Add
                    { From = FlowNodePortRef.producer link.FromNode fromPortName
                      To = { NodeId = transformer.Id; PortName = toPortName } }

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

    let private parseFlowBody
        (reader: TokenReader)
        (sources: ResizeArray<DashflowSourceDef>)
        (transformers: ResizeArray<DashflowTransformerDef>)
        (links: ResizeArray<FlowLinkDef>)
        (isDone: unit -> bool)
        =
        while not (isDone ()) && not reader.IsEof do
            reader.SkipNewlines()

            if isDone () then ()
            elif reader.TryKeyword "source" then
                let sourceId = reader.ReadIdent()
                sources.Add(parseSourceBlock reader sourceId)
            elif reader.TryKeyword "transformer" then
                let transformerId = reader.ReadIdent()
                transformers.Add(parseTransformerBlock reader transformerId)
            else
                let saved = reader.SavePosition()
                let nodeId = reader.ReadIdent()
                let fromPort = FlowLinkParser.tryReadBracketPortSameLine reader

                match FlowLinkParser.tryParseLink reader nodeId fromPort with
                | Some link -> links.Add link
                | None ->
                    reader.RestorePosition saved
                    raise (reader.Unexpected "source, transformer, or flow link")

    let private finishModule
        (flowId: string)
        (sources: DashflowSourceDef[])
        (transformers: DashflowTransformerDef[])
        (links: FlowLinkDef[])
        (typeCatalog: TypeCatalog)
        =
        let graph = buildGraph sources transformers links
        let diagnostics = FlowGraph.typeCheck graph typeCatalog

        { FlowId = flowId
          Sources = sources
          Transformers = transformers
          Links = links
          Graph = graph
          Diagnostics = diagnostics |> List.toArray }

    /// Body after <c>@flow &lt;id&gt;</c> until <c>end flow</c> (optional matching id on <c>end</c>).
    let private parseFlowEnvelope (reader: TokenReader) (flowId: string) (typeCatalog: TypeCatalog) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let sources = ResizeArray<DashflowSourceDef>()
        let transformers = ResizeArray<DashflowTransformerDef>()
        let links = ResizeArray<FlowLinkDef>()

        parseFlowBody
            reader
            sources
            transformers
            links
            (fun () -> BlockSyntax.isBlockEnd reader "flow" (Some flowId))

        BlockSyntax.expectBlockEnd reader "flow" (Some flowId)

        finishModule flowId (sources.ToArray()) (transformers.ToArray()) (links.ToArray()) typeCatalog

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
