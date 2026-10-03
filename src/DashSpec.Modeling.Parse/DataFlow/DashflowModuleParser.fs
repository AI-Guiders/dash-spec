namespace DashSpec.Modeling.Parse.DataFlow

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module DashflowModuleParser =

    let private readQualifiedName (reader: TokenReader) =
        let first = reader.ReadIdent()
        let mutable name = first

        while reader.IsAt TokenKind.Dot do
            reader.Advance()
            name <- $"{name}.{reader.ReadIdent()}"

        name

    let private parseWireAfterFrom (reader: TokenReader) =
        reader.ExpectKeyword "from"
        let qualified = reader.ReadIdent()

        match qualified.Split('.', 2, StringSplitOptions.RemoveEmptyEntries) with
        | [| nodeId; portName |] -> { NodeId = nodeId; PortName = portName }
        | _ ->
            raise (
                DashSpecParseException($"wire reference requires node.port after from, got '{qualified}'.")
            )

    let private readRowsOutput (reader: TokenReader) =
        if not (reader.TryKeyword "rows") then
            raise (DashSpecParseException("output requires rows <RowType>."))

        let typeName = reader.ReadIdent()

        if String.IsNullOrWhiteSpace typeName then
            raise (DashSpecParseException("output rows requires a type name."))

        typeName

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

    let private parseSourceBlock (reader: TokenReader) (sourceId: string) =
        reader.Expect TokenKind.LBrace
        reader.SkipNewlines()
        let mutable from: SourceFrom option = None
        let mutable outputPort = ""
        let mutable outputRowType = ""

        while not (reader.IsAt TokenKind.RBrace) && not reader.IsEof do
            reader.SkipNewlines()

            if reader.IsAt TokenKind.RBrace then ()
            elif reader.TryKeyword "from" then
                if reader.TryKeyword "view" then
                    let viewName = readQualifiedName reader
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
            elif reader.TryKeyword "output" then
                let portName = reader.ReadIdent()
                reader.Expect TokenKind.Colon
                let rowType = readRowsOutput reader
                outputPort <- portName
                outputRowType <- rowType
            else
                raise (reader.Unexpected "from or output")

            reader.SkipNewlines()

        reader.Expect TokenKind.RBrace

        match from with
        | None -> raise (DashSpecParseException($"source '{sourceId}' requires from view or from sql."))
        | Some value ->
            if String.IsNullOrWhiteSpace outputPort then
                raise (DashSpecParseException($"source '{sourceId}' requires output <port>: rows <Type>."))

            { Id = sourceId
              From = value
              OutputPort = outputPort
              OutputRowType = outputRowType }

    let private parseTransformerBlock (reader: TokenReader) (transformerId: string) =
        reader.Expect TokenKind.LBrace
        reader.SkipNewlines()
        let inputs = ResizeArray<string * FlowNodePortRef>()
        let outputs = ResizeArray<string * string>()

        while not (reader.IsAt TokenKind.RBrace) && not reader.IsEof do
            reader.SkipNewlines()

            if reader.IsAt TokenKind.RBrace then ()
            elif reader.TryKeyword "input" then
                let portName = reader.ReadIdent()
                let wire = parseWireAfterFrom reader
                inputs.Add((portName, wire))
            elif reader.TryKeyword "output" then
                let portName = reader.ReadIdent()
                reader.Expect TokenKind.Colon
                let rowType = readRowsOutput reader
                outputs.Add((portName, rowType))
            elif reader.TryKeyword "transform" then
                skipTransformStep reader
            else
                raise (reader.Unexpected "input, output, or transform")

            reader.SkipNewlines()

        reader.Expect TokenKind.RBrace

        { Id = transformerId
          Inputs = inputs.ToArray()
          Outputs = outputs.ToArray() }

    let private tryOutputType (nodes: IReadOnlyDictionary<string, FlowNode>) (ref: FlowNodePortRef) =
        match nodes.TryGetValue ref.NodeId with
        | false, _ -> None
        | true, node ->
            node.Outputs
            |> Array.tryFind (fun port -> String.Equals(port.Name, ref.PortName, StringComparison.OrdinalIgnoreCase))
            |> Option.map (fun port -> port.Type)

    let private buildGraph (sources: DashflowSourceDef[]) (transformers: DashflowTransformerDef[]) =
        let nodeList = ResizeArray<FlowNode>()
        let edges = ResizeArray<FlowEdge>()

        for source in sources do
            nodeList.Add
                { Id = source.Id
                  Kind = FlowNodeKind.Source
                  Inputs = Array.empty
                  Outputs =
                    [| { Name = source.OutputPort
                         Type = DashPortType.Rows source.OutputRowType } |] }

        for transformer in transformers do
            let outputPorts =
                transformer.Outputs
                |> Array.map (fun (name, rowType) -> { Name = name; Type = DashPortType.Rows rowType })

            nodeList.Add
                { Id = transformer.Id
                  Kind = FlowNodeKind.Transformer
                  Inputs =
                    transformer.Inputs
                    |> Array.map (fun (name, _) -> { Name = name; Type = DashPortType.Rows "" })
                  Outputs = outputPorts }

            for portName, wire in transformer.Inputs do
                edges.Add
                    { From = { NodeId = wire.NodeId; PortName = wire.PortName }
                      To = { NodeId = transformer.Id; PortName = portName } }

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

    let parseModule (text: string) (typeCatalog: TypeCatalog) =
        if String.IsNullOrWhiteSpace text then invalidArg "text" "Dashflow text is required."
        let reader = ParserUtilities.createReader text
        reader.SkipFileDirectives()
        reader.SkipNewlines()
        reader.Expect TokenKind.At
        reader.ExpectKeyword "flow"
        let flowId = reader.ReadIdent()

        if String.IsNullOrWhiteSpace flowId then
            raise (DashSpecParseException("@flow requires an id."))

        reader.SkipNewlines()
        let sources = ResizeArray<DashflowSourceDef>()
        let transformers = ResizeArray<DashflowTransformerDef>()

        while not reader.IsEof do
            reader.SkipNewlines()

            if reader.IsEof then ()
            elif reader.TryKeyword "source" then
                let sourceId = reader.ReadIdent()
                sources.Add(parseSourceBlock reader sourceId)
            elif reader.TryKeyword "transformer" then
                let transformerId = reader.ReadIdent()
                transformers.Add(parseTransformerBlock reader transformerId)
            else
                raise (reader.Unexpected "source or transformer")

        let graph = buildGraph (sources.ToArray()) (transformers.ToArray())
        let diagnostics = FlowGraph.typeCheck graph typeCatalog

        { FlowId = flowId
          Sources = sources.ToArray()
          Transformers = transformers.ToArray()
          Graph = graph
          Diagnostics = diagnostics |> List.toArray }
