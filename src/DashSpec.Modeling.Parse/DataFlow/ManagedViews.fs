namespace DashSpec.Modeling.Parse.DataFlow

open System
open System.Security.Cryptography
open System.Text
open DashSpec.Modeling.Core

/// <summary>ADR-0125 / ADR-0128: the managed-view emission SSOT in Modeling.Parse —
/// the deterministic content-addressed name (<c>dashspec_&lt;flow&gt;_&lt;chain&gt;_&lt;sha12&gt;</c>),
/// the canonical SELECT it is a function of, and the terminal row type, all computed
/// from the parsed .ds module alone. The reader (<c>DocumentFlowMaterializer</c>) and
/// the DDL emitter (Compilation) consume the same entries — no on-disk manifest, no
/// second source of truth that can go stale against the .ds.</summary>
module ManagedViews =

    /// One emitted managed view: chain terminal → name + canonical SELECT.
    type ManagedViewEntry =
        { Flow: string
          Chain: string
          ViewName: string
          Select: string
          RowType: string option
          IsFunction: bool
          /// iTVF parameters (name, SQL type, bound column, match kind).
          Params: (string * string * string * string) list }

    let private slug (value: string) =
        let builder = StringBuilder()
        let mutable lastWasSeparator = false

        for ch in value.ToLowerInvariant() do
            if Char.IsAsciiLetterOrDigit ch then
                builder.Append ch |> ignore
                lastWasSeparator <- false
            elif not lastWasSeparator then
                builder.Append '_' |> ignore
                lastWasSeparator <- true

        builder.ToString().TrimEnd '_'

    /// <summary>12 hex chars of SHA-256 over the whitespace-canonical SELECT —
    /// identical queries share one physical view, different logic never collides.</summary>
    let sha12 (select: string) =
        let canonical =
            select.Split([| ' '; '\r'; '\n'; '\t' |], StringSplitOptions.RemoveEmptyEntries)
            |> String.concat " "

        let hash = SHA256.HashData(Encoding.UTF8.GetBytes canonical)
        Convert.ToHexString(hash).[0..11].ToLowerInvariant()

    /// All transformer paths from a source to terminal nodes (each terminal = one view).
    let private chainsFrom (module': DashflowModule) (sourceId: string) =
        let chains = ResizeArray<DashflowTransformerDef list>()

        let rec walk (nodeId: string) (path: DashflowTransformerDef list) =
            let next =
                module'.Links
                |> Array.filter (fun link -> String.Equals(link.FromNode, nodeId, StringComparison.OrdinalIgnoreCase))

            if Array.isEmpty next then
                chains.Add(List.rev path)
            else
                for link in next do
                    match
                        module'.Transformers
                        |> Array.tryFind (fun t -> String.Equals(t.Id, link.ToNode, StringComparison.OrdinalIgnoreCase))
                    with
                    | Some transformer -> walk transformer.Id (transformer :: path)
                    | None -> () // nested-flow consumer: not a view chain terminal (v1)

        walk sourceId []
        List.ofSeq chains

    let private columnList (source: DashflowSourceDef) (catalog: TypeCatalog) =
        match TypeCatalog.tryGet catalog source.OutputRowType with
        | None -> "*"
        | Some def ->
            let columns =
                def.Fields
                |> Array.filter (fun f -> f.Type.IsPrimitive)
                |> Array.map (fun f -> if f.Name.Contains '.' then $"{f.Name} AS [{f.Name}]" else f.Name)

            if columns.Length = 0 then "*" else String.Join(", ", columns)

    /// ADR-0126 §2: composite join select list — every column rides a side-qualified
    /// expression over the l/r aliases; the composite row type is the schema contract.
    let private compositeColumnList (module': DashflowModule) (source: DashflowSourceDef) (catalog: TypeCatalog) =
        let compositeDef =
            match TypeCatalog.tryGet catalog source.OutputRowType with
            | Some def -> def
            | None ->
                raise (
                    DashSpecFlowException(
                        $"flow '{module'.FlowId}': composite source '{source.Id}' row type '{source.OutputRowType}' is not defined " +
                        "— annotate the join sides or the join itself with a row type so the merge has a contract."))

        let (leftId, rightId) =
            match source.Origin with
            | SourceOrigin.InnerJoin (l, r, _) -> (Some l, Some r)
            | SourceOrigin.LeftJoin (l, r, _) -> (Some l, Some r)
            | SourceOrigin.RightJoin (l, r, _) -> (Some l, Some r)
            | SourceOrigin.FullJoin (l, r, _) -> (Some l, Some r)
            | _ -> (None, None)

        let sideDef (id: string option) =
            match id with
            | None -> None
            | Some sideId ->
                module'.Sources
                |> Array.tryFind (fun s -> String.Equals(s.Id, sideId, StringComparison.OrdinalIgnoreCase))
                |> Option.bind (fun s ->
                    if String.IsNullOrWhiteSpace s.OutputRowType then None
                    else TypeCatalog.tryGet catalog s.OutputRowType)

        let leftDef = sideDef leftId
        let rightDef = sideDef rightId

        let hasField (def: RowTypeDef option) (name: string) =
            match def with
            | Some d -> d.Fields |> Array.exists (fun f -> String.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
            | None -> false

        compositeDef.Fields
        |> Array.filter (fun f -> f.Type.IsPrimitive)
        |> Array.map (fun field ->
            if field.Name.StartsWith("r.", StringComparison.OrdinalIgnoreCase) then
                $"{field.Name} AS [{field.Name}]"
            elif hasField leftDef field.Name then
                $"l.{field.Name}"
            elif hasField rightDef field.Name then
                $"r.{field.Name}"
            else
                field.Name)
        |> fun parts -> String.Join(", ", parts)

    let private isBound (port: DashflowParamPortDef) =
        match port.ColumnBinding with
        | Some value -> not (String.IsNullOrWhiteSpace value)
        | None -> false

    /// iTVF parameters + the WHERE fragment they produce (ADR-0120 binding, ADR-0125 §3).
    /// daterange ports expand into a half-open [from, to) pair — the shape the reader pushes.
    let private parametersOf (bound: DashflowParamPortDef list) =
        let mutable parameters = []
        let mutable predicates = []

        for port in bound do
            let column = (Option.get port.ColumnBinding).Trim()
            let name = slug port.Name

            match port.ValueType with
            | "daterange" ->
                parameters <-
                    (name + "_from", "DATE", column, "range")
                    :: (name + "_to", "DATE", column, "range")
                    :: parameters

                predicates <- $"{column} >= @{name}_from AND {column} < @{name}_to" :: predicates
            | "int" ->
                parameters <- (name, "INT", column, "eq") :: parameters
                predicates <- $"{column} = @{name}" :: predicates
            | _ ->
                parameters <- (name, "NVARCHAR(400)", column, "eq") :: parameters
                predicates <- $"{column} = @{name}" :: predicates

        (List.rev parameters, String.Join(" AND ", List.rev predicates))

    /// The chain's terminal row type — the inferred (ADR-0127) or declared output
    /// of the last transformer; the source type for raw chains.
    let private chainRowType (source: DashflowSourceDef) (chain: DashflowTransformerDef list) =
        match chain with
        | [] -> if String.IsNullOrWhiteSpace source.OutputRowType then None else Some source.OutputRowType
        | _ ->
            let terminal = chain |> List.rev |> List.head

            if Array.isEmpty terminal.Outputs then
                None
            else
                let (_, rowType) = terminal.Outputs[0]
                if String.IsNullOrWhiteSpace rowType then None else Some rowType

    let private stepParam (step: DashflowTransformStepDef) (key: string) =
        step.Parameters
        |> Array.tryFind (fun p -> String.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase))
        |> Option.map (fun p -> p.Value.Trim())

    /// The summarize step's SQL aggregate — the GroupByStepSpec contract:
    /// COUNT(*) for count (no value column), <c>AGG(value)</c> otherwise.
    let private summarizeAggregate (step: DashflowTransformStepDef) =
        let agg = stepParam step "agg" |> Option.defaultValue "count"

        if agg = "count" then
            let value = stepParam step "value" |> Option.defaultValue "*"
            $"COUNT({value})"
        else
            let value = stepParam step "value" |> Option.defaultValue "value"
            $"{agg.ToUpperInvariant()}({value})"

    /// The canonical SELECT of one chain (ADR-0125 §3): database-locality steps
    /// compile in (summarize → GROUP BY; order is consumed by the reader — a VIEW
    /// cannot carry ORDER BY); unknown database transforms return None — the chain
    /// stays on the reader's graph path. The WHERE rides the innermost SELECT.
    let private selectFor
        (module': DashflowModule)
        (source: DashflowSourceDef)
        (chain: DashflowTransformerDef list)
        (from: string)
        (catalog: TypeCatalog)
        (whereClause: string option)
        : string option =
        let columns =
            match source.Origin with
            | SourceOrigin.InnerJoin _ | SourceOrigin.LeftJoin _ | SourceOrigin.RightJoin _ | SourceOrigin.FullJoin _ ->
                compositeColumnList module' source catalog
            | _ -> columnList source catalog

        let withWhere =
            match whereClause with
            | Some where -> $"SELECT {columns}\nFROM {from}\nWHERE {where}"
            | None -> $"SELECT {columns}\nFROM {from}"

        let mutable current = withWhere
        let mutable compilable = true

        for transformer in chain do
            for step in transformer.Steps do
                if compilable
                   && String.Equals(
                       TransformDeclarations.localityOf step.PluginId,
                       "database",
                       StringComparison.OrdinalIgnoreCase) then
                    if String.Equals(step.PluginId, "summarize", StringComparison.OrdinalIgnoreCase) then
                        match stepParam step "by" with
                        | None -> compilable <- false // loud at declaration check; the chain rides the graph path
                        | Some by ->
                            let aggregate = summarizeAggregate step

                            current <-
                                $"SELECT {by} AS {by}, {aggregate} AS value\nFROM (\n{current}\n) AS src\nGROUP BY {by}"
                    elif not (String.Equals(step.PluginId, "order", StringComparison.OrdinalIgnoreCase)) then
                        compilable <- false

        if compilable then Some current else None

    /// Emit every source-chain of a view-marked module. Sources with no SQL
    /// material (xlsx / sql-file) are skipped; unbound param ports on a chain
    /// are loud — neither a flat VIEW nor an iTVF can carry a runtime filter.
    /// Composite row types (ADR-0127) synthesized at parse time ride the module —
    /// the emitter resolves them like any declared type (SSOT: the module).
    let emit (module': DashflowModule) (catalog: TypeCatalog) : ManagedViewEntry list =
        if not module'.EmitViews then
            []
        else
            let catalog =
                if Array.isEmpty module'.InferredRowTypes then
                    catalog
                else
                    // the ONE tolerant merge (TypeCatalog.withInferred) — the twin
                    // preference rule at catalog assembly
                    TypeCatalog.withInferred $"flow '{module'.FlowId}'" module'.InferredRowTypes catalog

            [
                for source in module'.Sources do
                    // A managed union never rides SELECT * — the canonical projection is
                    // the union row type's field order (ADR-0126 §1).
                    let unionColumns =
                        match source.Origin with
                        | SourceOrigin.UnionAll _ ->
                            TypeCatalog.tryGet catalog source.OutputRowType
                            |> Option.map (fun def ->
                                def.Fields
                                |> Array.filter (fun f -> f.Type.IsPrimitive)
                                |> Array.map (fun f -> f.Name)
                                |> fun names -> String.Join(", ", names))
                        | _ -> None

                    match CompositeSql.fromClause module' source unionColumns with
                    | None -> () // xlsx / sql-file chains do not emit views (v1)
                    | Some from ->
                        for chain in chainsFrom module' source.Id do
                            let chainId =
                                if List.isEmpty chain then source.Id
                                else (chain |> List.rev |> List.head).Id

                            let rowType = chainRowType source chain

                            let allParams = chain |> List.collect (fun t -> List.ofArray t.Params)
                            let bound = allParams |> List.filter isBound

                            if allParams.Length > bound.Length then
                                let unbound = allParams |> List.find (not << isBound)

                                raise (
                                    DashSpecFlowException(
                                        $"flow '{module'.FlowId}': param port '{unbound.Name}' has no column binding — a flat VIEW " +
                                        "cannot carry a runtime filter and an inline function requires the bound column. " +
                                        "Add 'bind <column>' to the param port or remove the param port from the view chain."))

                            if not (List.isEmpty bound) then
                                let (parameters, where) = parametersOf bound

                                match selectFor module' source chain from catalog (Some where) with
                                | Some select ->
                                    let viewName = $"dashspec_{slug module'.FlowId}_{slug chainId}_{sha12 select}"

                                    yield
                                        { Flow = module'.FlowId
                                          Chain = chainId
                                          ViewName = viewName
                                          Select = select
                                          RowType = rowType
                                          IsFunction = true
                                          Params = parameters }
                                | None ->
                                    raise (
                                        DashSpecFlowException(
                                            $"flow '{module'.FlowId}': parameterized chain from '{source.Id}' failed to compile."))
                            else
                                match selectFor module' source chain from catalog None with
                                | Some select ->
                                    let viewName = $"dashspec_{slug module'.FlowId}_{slug chainId}_{sha12 select}"

                                    yield
                                        { Flow = module'.FlowId
                                          Chain = chainId
                                          ViewName = viewName
                                          Select = select
                                          RowType = rowType
                                          IsFunction = false
                                          Params = [] }
                                | None ->
                                    raise (
                                        DashSpecFlowException(
                                            $"flow '{module'.FlowId}': chain from source '{source.Id}' cannot compile into a flat VIEW " +
                                            "(add 'bind <column>' to parameterized ports in the chain).")) ]

    /// The view for a card's flow input: the terminal node id IS the chain id.
    let tryFind (entries: ManagedViewEntry list) (flowId: string) (nodeId: string) =
        entries
        |> List.tryFind (fun v ->
            String.Equals(v.Flow, flowId, StringComparison.OrdinalIgnoreCase)
            && String.Equals(v.Chain, nodeId, StringComparison.OrdinalIgnoreCase))
