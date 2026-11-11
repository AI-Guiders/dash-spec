namespace DashSpec.Modeling.Parse.DataFlow

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Syntax

module DashflowModuleParser =

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

    /// <summary>ADR-0126 §1.1: origin-expression AST — constructor ident + args,
    /// transpiled into a <c>SourceOrigin</c> DU case after the body is read
    /// (join conditions live in the block body, not in the args).</summary>
    type private ParsedOrigin =
        | PLeaf of kind: SourceFromKind * value: string * sheet: string option
        | PComposite of kind: CompositeOriginKind * left: string * right: string

    and private CompositeOriginKind =
        | JoinInner
        | JoinLeft
        | JoinRight
        | JoinFull
        | Union

    let private readIdentList (reader: TokenReader) =
        let items = ResizeArray<string>()
        items.Add(reader.ReadIdent())

        while reader.CurrentKind = TokenKind.Comma do
            reader.Advance()
            items.Add(reader.ReadIdent())

        items.ToArray()

    let private parseOriginArgs (reader: TokenReader) (sourceId: string) (ctor: string) =
        if reader.CurrentKind <> TokenKind.LParen then
            raise (DashSpecParseException($"source '{sourceId}': origin '{ctor}' requires parenthesized arguments."))

        reader.Expect TokenKind.LParen
        let args = readIdentList reader
        reader.Expect TokenKind.RParen
        args

    let private parseOriginExpr (reader: TokenReader) (sourceId: string) =
        let ctor = reader.ReadIdent()

        if String.Equals(ctor, "view", StringComparison.OrdinalIgnoreCase) then
            if reader.CurrentKind <> TokenKind.LParen then
                raise (DashSpecParseException($"source '{sourceId}': origin 'View' requires a parenthesized dotted view name."))

            reader.Expect TokenKind.LParen
            let viewName = AccessorGrammar.readDotted reader
            reader.Expect TokenKind.RParen
            PLeaf(SourceFromKind.View, viewName, None)
        elif String.Equals(ctor, "sqlquery", StringComparison.OrdinalIgnoreCase) then
            if reader.CurrentKind <> TokenKind.LParen then
                raise (DashSpecParseException($"source '{sourceId}': origin 'SqlQuery' requires parenthesized arguments."))

            reader.Expect TokenKind.LParen

            let body =
                match reader.CurrentKind with
                | TokenKind.String -> reader.ReadString()
                | TokenKind.Raw -> reader.ReadRawBlock()
                | _ -> raise (reader.Unexpected "sql query string")

            reader.Expect TokenKind.RParen
            PLeaf(SourceFromKind.SqlQuery, body, None)
        elif String.Equals(ctor, "sqlfile", StringComparison.OrdinalIgnoreCase) then
            let args = parseOriginArgs reader sourceId "SqlFile"
            let path = args |> Array.exactlyOne
            PLeaf(SourceFromKind.SqlFile, path, None)
        elif String.Equals(ctor, "xlsx", StringComparison.OrdinalIgnoreCase) then
            let args = parseOriginArgs reader sourceId "Xlsx"
            let path = args |> Array.exactlyOne
            PLeaf(SourceFromKind.Xlsx, path, None)
        elif
            String.Equals(ctor, "innerjoin", StringComparison.OrdinalIgnoreCase)
            || String.Equals(ctor, "leftjoin", StringComparison.OrdinalIgnoreCase)
            || String.Equals(ctor, "rightjoin", StringComparison.OrdinalIgnoreCase)
            || String.Equals(ctor, "fulljoin", StringComparison.OrdinalIgnoreCase)
            || String.Equals(ctor, "unionall", StringComparison.OrdinalIgnoreCase)
        then
            let args = parseOriginArgs reader sourceId ctor

            if args.Length <> 2 then
                raise (DashSpecParseException($"source '{sourceId}': {ctor} takes exactly two existing source ids (got {args.Length} args)."))

            let kind =
                if String.Equals(ctor, "innerjoin", StringComparison.OrdinalIgnoreCase) then JoinInner
                elif String.Equals(ctor, "leftjoin", StringComparison.OrdinalIgnoreCase) then JoinLeft
                elif String.Equals(ctor, "rightjoin", StringComparison.OrdinalIgnoreCase) then JoinRight
                elif String.Equals(ctor, "fulljoin", StringComparison.OrdinalIgnoreCase) then JoinFull
                else Union

            PComposite(kind, args.[0], args.[1])
        else
            raise (
                DashSpecParseException(
                    $"source '{sourceId}': unknown origin '{ctor}' (View, SqlQuery, SqlFile, Xlsx, InnerJoin, LeftJoin, RightJoin, FullJoin, UnionAll)."))

    let private parseSourceBlock (reader: TokenReader) (sourceId: string) =
        // ADR-0126 §1: `source <id> [: RowType] [= Origin] { body }` — the
        // annotation and origin constructor are the new-form header; legacy
        // `from view/sql/xlsx` bodies transpile on read.
        let mutable annotationRowType: string option = None

        if reader.CurrentKind = TokenKind.Colon then
            reader.Advance()
            annotationRowType <- Some(reader.ReadIdent())

        let mutable headerOrigin: ParsedOrigin option = None

        if reader.CurrentKind = TokenKind.Eq then
            reader.Advance()
            headerOrigin <- Some(parseOriginExpr reader sourceId)

        // ADR-0126 §1: a composite origin may carry no body at all
        // (`source all = UnionAll(a, b)`) — conditions would live in `{}`.
        let hasBody = reader.CurrentKind = TokenKind.LBrace

        if not hasBody then
            match headerOrigin with
            | Some(PComposite _) -> ()
            | _ -> raise (reader.Unexpected "'{' after source header")
        else
            reader.Expect TokenKind.LBrace

        reader.SkipNewlines()
        let mutable providerBinding = None
        let mutable from: SourceFrom option = None
        let mutable onKeys: string list = []
        let mutable leftKeys: string list = []
        let mutable rightKeys: string list = []
        let mutable timeZone = ""
        let mutable outputPort = ""
        let mutable outputRowType = ""
        let mutable defaultOutputPort = None

        while hasBody && not (reader.IsAt TokenKind.RBrace) && not reader.IsEof do
            reader.SkipNewlines()

            if reader.IsAt TokenKind.RBrace then ()
            elif reader.TryKeyword "time_zone" then
                reader.Expect TokenKind.Eq

                // `utc` лексер отдаёт как TimeShift-литерал (та же форма, что UTC+3), поэтому
                // кроме идентификатора принимаем и TimeShift-токен.
                let raw =
                    match reader.CurrentKind with
                    | TokenKind.Ident -> reader.ReadIdent()
                    | TokenKind.TimeShift -> reader.ReadTimeShift()
                    | TokenKind.String -> reader.ReadString()
                    | _ -> raise (reader.Unexpected "time_zone value utc or local")

                let value = raw.Trim().ToLowerInvariant()

                if value <> "utc" && value <> "local" then
                    raise (
                        DashSpecParseException(
                            $"source '{sourceId}': time_zone supports utc or local (got '{raw}')."))

                timeZone <- value
            elif reader.TryKeyword "on" then
                reader.Expect TokenKind.Eq
                onKeys <- readIdentList reader |> List.ofArray
            elif reader.TryKeyword "left_key" then
                reader.Expect TokenKind.Eq
                leftKeys <- readIdentList reader |> List.ofArray
            elif reader.TryKeyword "right_key" then
                reader.Expect TokenKind.Eq
                rightKeys <- readIdentList reader |> List.ofArray
            elif reader.TryKeyword "use" then
                let useKind = reader.ReadIdent()

                if String.Equals(useKind, "connector", StringComparison.OrdinalIgnoreCase) then
                    raise (DashSpecParseException($"source '{sourceId}': use 'provider <id>', not 'connector' (connector is the runtime plugin; provider selects the manifest entry)."))

                if not (String.Equals(useKind, "provider", StringComparison.OrdinalIgnoreCase)) then
                    raise (DashSpecParseException($"source '{sourceId}' supports only 'use provider <id|infer>' before from/ports."))

                match headerOrigin with
                | Some(PComposite _) ->
                    raise (
                        DashSpecParseException(
                            $"source '{sourceId}': a composite source inherits use provider from its constituents — declare it on them, not here."))
                | _ -> ()

                let providerName = reader.ReadIdent()

                if String.IsNullOrWhiteSpace providerName then
                    raise (DashSpecParseException($"source '{sourceId}': use provider requires an id or infer."))

                providerBinding <-
                    if String.Equals(providerName, "infer", StringComparison.OrdinalIgnoreCase) then
                        Some DashflowProviderBinding.Infer
                    else
                        Some(DashflowProviderBinding.Named providerName)
            elif reader.TryKeyword "ports" then
                match headerOrigin with
                | Some(PComposite _) ->
                    raise (
                        DashSpecParseException(
                            $"source '{sourceId}': a composite source has no ports block — annotate the row type in the header (`source {sourceId}: Type = …`) or leave it for synthesis."))
                | _ -> ()

                let ports = FlowPortsParser.parsePortsBlock reader
                defaultOutputPort <- applySourcePorts ports &outputPort &outputRowType
            elif reader.TryKeyword "from" then
                if headerOrigin.IsSome then
                    raise (
                        DashSpecParseException(
                            $"source '{sourceId}': origin already declared in the header (`= …`) — 'from' cannot be declared twice."))
                if reader.TryKeyword "view" then
                    let viewName = AccessorGrammar.readDotted reader
                    from <- Some { Kind = SourceFromKind.View; Value = viewName; Sheet = None }
                elif reader.TryKeyword "sql" then
                    if reader.TryKeyword "query" then
                        let body =
                            match reader.CurrentKind with
                            | TokenKind.String -> reader.ReadString()
                            | TokenKind.Raw -> reader.ReadRawBlock()
                            | _ -> raise (reader.Unexpected "sql query string")

                        from <- Some { Kind = SourceFromKind.SqlQuery; Value = body; Sheet = None }
                    elif reader.TryKeyword "file" then
                        let path = reader.ReadString()
                        from <- Some { Kind = SourceFromKind.SqlFile; Value = path; Sheet = None }
                    else
                        raise (DashSpecParseException("from sql requires query or file."))
                elif reader.TryKeyword "xlsx" then
                    if not (reader.TryKeyword "file") then
                        raise (DashSpecParseException("from xlsx requires file."))

                    let path = reader.ReadString()
                    let sheet =
                        if reader.TryKeyword "sheet" then Some(reader.ReadString()) else None

                    from <- Some { Kind = SourceFromKind.Xlsx; Value = path; Sheet = sheet }
                else
                    raise (DashSpecParseException("source from requires view, sql, or xlsx."))
            else
                raise (reader.Unexpected "from or ports")

            reader.SkipNewlines()

        if hasBody then reader.Expect TokenKind.RBrace

        // ADR-0126 §1: assemble the origin — header `= Origin` or legacy `from`.
        let origin: SourceOrigin option =
            match headerOrigin, from with
            | Some(PLeaf(kind, value, sheet)), None ->
                if not onKeys.IsEmpty || not leftKeys.IsEmpty || not rightKeys.IsEmpty then
                    raise (
                        DashSpecParseException(
                            $"source '{sourceId}': `on` / `left_key` / `right_key` are only valid on composite sources (InnerJoin/LeftJoin/RightJoin/FullJoin/UnionAll)."))

                Some(SourceOrigin.Leaf(kind, value, sheet))
            | Some(PComposite(kind, left, right)), None ->
                let joinCond =
                    // join condition: either a shared `on` or explicit left_key+right_key
                    if not onKeys.IsEmpty then
                        if not leftKeys.IsEmpty || not rightKeys.IsEmpty then
                            raise (
                                DashSpecParseException(
                                    $"source '{sourceId}': `on` and `left_key`/`right_key` cannot be mixed — pick one join-condition form."))

                        Some { On = onKeys; LeftKey = []; RightKey = [] }
                    elif not leftKeys.IsEmpty || not rightKeys.IsEmpty then
                        if leftKeys.IsEmpty || rightKeys.IsEmpty then
                            raise (
                                DashSpecParseException(
                                    $"source '{sourceId}': join origin with `left_key` requires `right_key` too (and vice versa)."))

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
                        raise (
                            DashSpecParseException(
                                $"source '{sourceId}': UnionAll takes no join condition (`on` / `left_key` / `right_key` are not allowed)."))

                    Some(SourceOrigin.UnionAll(left, right))
                | _ ->
                    match joinCond with
                    | None ->
                        raise (
                            DashSpecParseException(
                                $"source '{sourceId}': join origin requires `on = col` or `left_key = …` + `right_key = …` in the body."))
                    | Some cond -> Some(join left right cond)
            | Some _, Some _ -> None // unreachable: the body rejects `from` when the header origin exists
            | None, Some legacy ->
                if not onKeys.IsEmpty || not leftKeys.IsEmpty || not rightKeys.IsEmpty then
                    raise (
                        DashSpecParseException(
                            $"source '{sourceId}': `on` / `left_key` / `right_key` are only valid on composite sources (InnerJoin/LeftJoin/RightJoin/FullJoin/UnionAll)."))

                Some(SourceOrigin.Leaf(legacy.Kind, legacy.Value, legacy.Sheet))
            | None, None -> raise (DashSpecParseException($"source '{sourceId}' requires an origin: `= View/SqlQuery/SqlFile/Xlsx/…` in the header or legacy `from view|sql|xlsx` in the body."))

        match origin with
        | None -> raise (DashSpecParseException($"source '{sourceId}' requires an origin."))
        | Some originValue ->
            let isComposite =
                match originValue with
                | SourceOrigin.Leaf _ -> false
                | _ -> true

            // ADR-0126 §1: leaf row type — ports block wins on agreement; a
            // mismatch with the header annotation is a loud contract break.
            if not isComposite then
                match annotationRowType with
                | Some ann when
                    not (String.IsNullOrWhiteSpace outputRowType)
                    && not (String.Equals(ann, outputRowType, StringComparison.Ordinal))
                    ->
                    raise (
                        DashSpecParseException(
                            $"source '{sourceId}': row type annotated in the header ('{ann}') differs from the ports block ('{outputRowType}') — declare one."))

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
                    // new-form sources have no ports block — the single output
                    // port name is decoration (ADR-0126 §1).
                    "rows"
                else
                    ""

            match providerBinding with
            | None when not isComposite ->
                raise (
                    DashSpecParseException(
                        $"source '{sourceId}' requires use provider <id> or use provider infer (manifest default_provider_id; same as card datasource infer)."))
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

    let private parseTransformerBlock (reader: TokenReader) (transformerId: string) =
        reader.Expect TokenKind.LBrace
        reader.SkipNewlines()
        let inputs = ResizeArray<DashflowInputDecl>()
        let outputs = ResizeArray<string * string>()
        let paramsDecls = ResizeArray<DashflowParamPortDef>()
        let steps = ResizeArray<DashflowTransformStepDef>()
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

                for decl in ports.Params do
                    paramsDecls.Add(
                        { Name = decl.Name
                          ValueType = decl.ValueType
                          Default = decl.Default
                          DependsOn = None
                          ColumnBinding = decl.ColumnBinding })
            elif reader.TryKeyword "transform" then
                steps.Add(TransformStepParser.parseTransformUseBlock reader transformerId)
            else
                raise (reader.Unexpected "ports or transform")

            reader.SkipNewlines()

        reader.Expect TokenKind.RBrace

        { Id = transformerId
          Inputs = inputs.ToArray()
          Outputs = outputs.ToArray()
          Params = paramsDecls.ToArray()
          DefaultInputPort = defaultInputPort
          DefaultOutputPort = defaultOutputPort
          Steps = steps.ToArray() }

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

    /// <summary>ADR-0127 §4: a ports-less transformer gets the default unary
    /// ports — input <c>raw</c>, output <c>rows</c> — with the output type left
    /// empty for inference to fill. Binary transforms (join/union) are not in the
    /// vocabulary yet; they will default to left/right/rows when declared.</summary>
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
        // ADR-0126 §1.1: composite sources type first — their synthesized row types
        // join the catalog before the transformer walk so consumers can resolve them.
        let sources, compositeSynthesized, compositeDiagnostics =
            TypeInference.resolveCompositeSources sources typeCatalog

        let compositeCatalog =
            if compositeSynthesized.IsEmpty then
                typeCatalog
            else
                TypeCatalog.ofDefinitions (
                    seq {
                        yield! typeCatalog.Definitions.Values
                        yield! compositeSynthesized
                    }
                )

        let outcome = TypeInference.inferScope sources transformers links compositeCatalog
        let allSynthesized = compositeSynthesized @ outcome.Synthesized

        // patch untyped outputs with the inferred names (declared outputs keep their names —
        // they were verified by the transform-typecheck inside the inference)
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

        let extendedCatalog =
            if allSynthesized.IsEmpty then
                compositeCatalog
            else
                TypeCatalog.ofDefinitions (
                    seq {
                        yield! compositeCatalog.Definitions.Values
                        yield! outcome.Synthesized
                    }
                )

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

    let private tryReadIdentListFromProperty (prop: PropertySyntax) : string list option =
        let items = ResizeArray<string>()
        let mutable ok = true
        let mutable idx = 0

        while ok && idx < prop.ValueTokens.Length do
            if prop.ValueTokens.[idx].LexKind = TokenKind.Ident then
                items.Add prop.ValueTokens.[idx].Text
                idx <- idx + 1
                if idx < prop.ValueTokens.Length then
                    if prop.ValueTokens.[idx].LexKind = TokenKind.Comma then
                        idx <- idx + 1
                    else
                        ok <- false
            else
                ok <- false

        if ok && items.Count > 0 then Some(List.ofSeq items) else None

    let private tryProjectSourceFromAst (sourceId: string) (block: BlockDeclarationSyntax) : DashflowSourceDef option =
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
        let mutable ok = true

        for memberNode in block.Members do
            if ok then
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
                        match tryReadIdentListFromProperty prop with
                        | Some keys -> onKeys <- keys
                        | None -> ok <- false
                    elif String.Equals(prop.Key, "left_key", StringComparison.OrdinalIgnoreCase) then
                        match tryReadIdentListFromProperty prop with
                        | Some keys -> leftKeys <- keys
                        | None -> ok <- false
                    elif String.Equals(prop.Key, "right_key", StringComparison.OrdinalIgnoreCase) then
                        match tryReadIdentListFromProperty prop with
                        | Some keys -> rightKeys <- keys
                        | None -> ok <- false
                    else
                        ok <- false
                | DashSpecAstNode.ClauseDeclaration clause ->
                    match clause.Kind with
                    | ClauseKind.UseClause(useKind, target) ->
                        if String.Equals(useKind, "connector", StringComparison.OrdinalIgnoreCase) then
                            raise (DashSpecParseException($"source '{sourceId}': use 'provider <id>', not 'connector' (connector is the runtime plugin; provider selects the manifest entry)."))

                        if not (String.Equals(useKind, "provider", StringComparison.OrdinalIgnoreCase)) then
                            raise (DashSpecParseException($"source '{sourceId}' supports only 'use provider <id|infer>' before from/ports."))

                        match headerOrigin with
                        | Some(PComposite _) ->
                            raise (DashSpecParseException($"source '{sourceId}': a composite source inherits use provider from its constituents — declare it on them, not here."))
                        | _ -> ()

                        providerBinding <-
                            if String.Equals(target, "infer", StringComparison.OrdinalIgnoreCase) then
                                Some DashflowProviderBinding.Infer
                            else
                                Some(DashflowProviderBinding.Named target)
                    | ClauseKind.FromClause originSyntax ->
                        if headerOrigin.IsSome then
                            raise (DashSpecParseException($"source '{sourceId}': origin already declared in the header (`= …`) — 'from' cannot be declared twice."))

                        match originSyntax with
                        | SourceOriginSyntax.Leaf(k, value, sheet) ->
                            from <- Some { Kind = mapLeafKind k; Value = value; Sheet = sheet }
                        | _ -> ok <- false
                    | _ -> ok <- false
                | DashSpecAstNode.BlockDeclaration childBlock ->
                    match childBlock.Opener with
                    | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Ports
                    | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Ports, _) ->
                        match headerOrigin with
                        | Some(PComposite _) ->
                            raise (DashSpecParseException($"source '{sourceId}': a composite source has no ports block — annotate the row type in the header (`source {sourceId}: Type = …`) or leave it for synthesis."))
                        | _ -> ()

                        match FlowPortsParser.tryProjectPortsFromAst childBlock.Members with
                        | Some ports ->
                            defaultOutputPort <- applySourcePorts ports &outputPort &outputRowType
                        | None -> ok <- false
                    | _ -> ok <- false
                | DashSpecAstNode.BlankLine _
                | DashSpecAstNode.EndBlock _ -> ()
                | memberNode when DashSpecAst.isTrivia memberNode -> ()
                | _ -> ok <- false

        if not ok then
            None
        else
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

                Some
                    { Id = sourceId
                      Provider = match providerBinding with Some b -> b | None -> DashflowProviderBinding.Infer
                      Origin = originValue
                      TimeZone = timeZone
                      OutputPort = finalPort
                      OutputRowType = finalRowType
                      DefaultOutputPort = defaultOutputPort }

    let private tryProjectTransformerFromAst (transformerId: string) (block: BlockDeclarationSyntax) : DashflowTransformerDef option =
        let inputs = ResizeArray<DashflowInputDecl>()
        let outputs = ResizeArray<string * string>()
        let paramsDecls = ResizeArray<DashflowParamPortDef>()
        let steps = ResizeArray<DashflowTransformStepDef>()
        let mutable defaultInputPort = None
        let mutable defaultOutputPort = None
        let mutable ok = true

        for memberNode in block.Members do
            if ok then
                match memberNode with
                | DashSpecAstNode.BlockDeclaration childBlock ->
                    match childBlock.Opener with
                    | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Ports
                    | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Ports, _) ->
                        match FlowPortsParser.tryProjectPortsFromAst childBlock.Members with
                        | None -> ok <- false
                        | Some ports ->
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
                        match TransformStepParser.tryProjectStepFromAst transformerId pluginId childBlock with
                        | Some step -> steps.Add step
                        | None -> ok <- false
                    | _ -> ok <- false
                | DashSpecAstNode.BlankLine _
                | DashSpecAstNode.EndBlock _ -> ()
                | memberNode when DashSpecAst.isTrivia memberNode -> ()
                | _ -> ok <- false

        if not ok then
            None
        else
            Some
                { Id = transformerId
                  Inputs = inputs.ToArray()
                  Outputs = outputs.ToArray()
                  Params = paramsDecls.ToArray()
                  DefaultInputPort = defaultInputPort
                  DefaultOutputPort = defaultOutputPort
                  Steps = steps.ToArray() }

    /// <summary>One flow scope from typed tree members: Source, Transformer, FlowLink,
    /// and nested Flow blocks project directly from typed AST nodes without rebuilding
    /// any TokenReader stream.</summary>
    let rec private tryProjectScope
        (members: DashSpecAstNode[])
        (typeCatalog: TypeCatalog)
        (seenIds: HashSet<string>)
        : (DashflowSourceDef[] * DashflowTransformerDef[] * DashflowNestedFlowDef[] * FlowLinkDef[]) option
        =
        let sources = ResizeArray<DashflowSourceDef>()
        let transformers = ResizeArray<DashflowTransformerDef>()
        let nestedFlows = ResizeArray<DashflowNestedFlowDef>()
        let links = ResizeArray<FlowLinkDef>()
        let mutable ok = true

        for memberNode in members do
            if ok then
                match memberNode with
                | DashSpecAstNode.BlockDeclaration block ->
                    match block.Opener with
                    | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Source, sourceId) ->
                        ensureUniqueNodeId "source" sourceId seenIds
                        match tryProjectSourceFromAst sourceId block with
                        | Some src -> sources.Add src
                        | None -> ok <- false
                    | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Transformer, transformerId) ->
                        ensureUniqueNodeId "transformer" transformerId seenIds
                        match tryProjectTransformerFromAst transformerId block with
                        | Some tr -> transformers.Add tr
                        | None -> ok <- false
                    | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Other "flow", nestedId) ->
                        ensureUniqueNodeId "nested flow" nestedId seenIds

                        match tryProjectScope block.Members typeCatalog seenIds with
                        | Some (s, t, n, l) ->
                            nestedFlows.Add(finishNestedFlow nestedId s t n l typeCatalog)
                        | None -> ok <- false
                    | _ -> ok <- false
                | DashSpecAstNode.FlowLinkDeclaration link ->
                    links.Add
                        { FromNode = link.FromNode
                          FromPort = link.FromPort
                          ToNode = link.ToNode
                          ToPort = link.ToPort }
                | DashSpecAstNode.ClauseDeclaration clause ->
                    match clause.Kind with
                    | ClauseKind.NamespaceClause _ -> ()
                    | _ -> ok <- false
                | DashSpecAstNode.ImportDeclaration _
                | DashSpecAstNode.BlankLine _
                | DashSpecAstNode.EndBlock _ -> ()
                | memberNode when DashSpecAst.isTrivia memberNode -> ()
                | _ -> ok <- false

        if ok then
            Some(sources.ToArray(), transformers.ToArray(), nestedFlows.ToArray(), links.ToArray())
        else
            None

    /// <summary>ADR-0130: project a flow module directly from the typed syntax tree.</summary>
    let private tryProjectFlowFromAst
        (root: DashSpecAstNode)
        (typeCatalog: TypeCatalog)
        : DashflowModule option =
        match
            DashSpecAst.descendants root
            |> Seq.tryPick (function
                | DashSpecAstNode.ModuleDeclaration m as node ->
                    match m.Directive with
                    | DashSpecModuleDirective.Unit("flow", flowId) -> Some(node, flowId)
                    | _ -> None
                | _ -> None)
        with
        | None -> None
        | Some (moduleNode, flowId) ->
            let emitViews =
                DashSpecAst.tokens moduleNode
                |> Array.exists (fun t -> t.LexKind = TokenKind.Ident && String.Equals(t.Text, "view", StringComparison.OrdinalIgnoreCase))

            let seenIds = HashSet<string>(StringComparer.OrdinalIgnoreCase)

            match tryProjectScope (DashSpecAst.members moduleNode) typeCatalog seenIds with
            | None -> None
            | Some (sources, transformers, nestedFlows, links) ->
                let parsed = finishModule flowId sources transformers nestedFlows links typeCatalog

                Some(if emitViews then { parsed with EmitViews = true } else parsed)

    /// `.dashflow` fragment: <c>@flow &lt;id&gt;</c> … <c>end flow</c>.
    let private legacyParseModule (text: string) (typeCatalog: TypeCatalog) =
        let reader = ParserUtilities.createReader text
        reader.SkipFileDirectives()
        reader.SkipNewlines()

        if reader.TryKeyword "dataflow" then
            raise (DashSpecParseException("use '@flow <id>' … 'end flow', not 'dataflow'."))

        if reader.TryKeyword "flow" then
            raise (DashSpecParseException("dashflow modules start with '@flow <id>', not a bare 'flow' keyword."))

        reader.ExpectKeyword "module"
        reader.ExpectKeyword "flow"
        let flowId = reader.ReadIdent()

        if String.IsNullOrWhiteSpace flowId then
            raise (DashSpecParseException("@flow requires an id."))

        let emitViews = reader.TryKeyword "view"

        reader.SkipNewlines()
        let parsed = parseFlowEnvelope reader flowId typeCatalog
        if emitViews then { parsed with EmitViews = true } else parsed

    let parseModule (text: string) (typeCatalog: TypeCatalog) =
        if String.IsNullOrWhiteSpace text then invalidArg "text" "Dashflow text is required."

        let tree = DashSpecSurfaceSyntax.parse text

        match tryProjectFlowFromAst tree.Root typeCatalog with
        | Some module' -> module'
        | None -> legacyParseModule text typeCatalog
