namespace DashSpec.Modeling.Parse.DataFlow

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Core.TransformDeclarations

/// <summary>
/// Transform port type inference (ADR-0127). The source node is the explicit
/// typing boundary (<c>output stream raw: SessionRow</c>); from there every
/// transformer's port types are computed from the incoming edge and the declared
/// type-effect of each step (<c>Std.Transformations</c> declarations). Explicit
/// output annotations (<c>output stream rows: MyType</c>) are not switches — they
/// are verified contracts: the compiler checks the inferred type against them
/// (transform-typecheck, DFLOW010).
///
/// Inferred structural types materialize as ordinary named <see cref="RowTypeDef"/>s
/// with deterministic human-readable names (§2: identity steps pass through, only
/// projection/extend/compose generate names; a hand-written structural twin in the
/// catalog is preferred over synthesis).
/// </summary>
module TypeInference =

    type InferenceDiagnostic =
        { Code: string
          Message: string }

    /// <summary>Per-scope inference outcome: port patches for the graph,
    /// synthesized row types (to merge into the document's type catalog), and
    /// diagnostics (DFLOW010 transform-typecheck, DFLOW011 opaque without a
    /// declared output, DFLOW012 unknown step column).</summary>
    type InferenceOutcome =
        { OutputTypes: IReadOnlyDictionary<string * string, string>
          Synthesized: RowTypeDef list
          Diagnostics: InferenceDiagnostic list }

    let private diag code message = { Code = code; Message = message }

    // --- naming (ADR-0127 §2) -------------------------------------------------

    let private stemOf (typeName: string) =
        if typeName.EndsWith("Row", StringComparison.OrdinalIgnoreCase) then
            typeName.Substring(0, typeName.Length - 3)
        else
            typeName

    let private capitalize (value: string) =
        if String.IsNullOrEmpty value then value
        else Char.ToUpperInvariant(value.[0]).ToString() + value.Substring(1)

    /// snake_case / dotted column → Pascal identifier segment (app_name → AppName).
    let private pascalOf (column: string) =
        column.Split([| '_'; '.' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map capitalize
        |> String.concat ""

    let private baseNameForEffect (inputStem: string) (step: DashflowTransformStepDef) =
        match TransformDeclarations.effectOf step.PluginId with
        | TransformTypeEffect.Projection ->
            // The by-column distinguishes same-stem projections (Model+domain,
            // Model+released) — without it every summarize collapses onto
            // <Stem>SummaryRow and the dedup suffix ships meaningless 2/3.
            let by =
                step.Parameters
                |> Array.tryFind (fun p -> String.Equals(p.Key, "by", StringComparison.OrdinalIgnoreCase))
                |> Option.map (fun p -> pascalOf p.Value)
                |> Option.defaultValue ""

            if String.IsNullOrEmpty by then $"{inputStem}SummaryRow" else $"{inputStem}By{by}Row"
        | TransformTypeEffect.Extend ->
            // window-style: <Base>№ColRow — the added column rides the step's `as`.
            let col =
                step.Parameters
                |> Array.tryFind (fun p -> String.Equals(p.Key, "as", StringComparison.OrdinalIgnoreCase))
                |> Option.map (fun p -> capitalize p.Value)
                |> Option.defaultValue ""

            if String.IsNullOrEmpty col then $"{inputStem}ExtendedRow" else $"{inputStem}{col}Row"
        | _ -> $"{inputStem}OutRow"

    // --- step application ------------------------------------------------------

    /// <summary>Apply one step's type-effect to the current row type. Returns the
    /// output type name (existing or freshly synthesized) or None when inference
    /// cannot proceed (opaque/unknown effect — explicit typing required).</summary>
    let private applyStep
        (catalog: TypeCatalog)
        (synthesized: ResizeArray<RowTypeDef>)
        (takenNames: HashSet<string>)
        (diagnostics: ResizeArray<InferenceDiagnostic>)
        (transformerId: string)
        (currentType: string)
        (step: DashflowTransformStepDef)
        : string option =
        let paramValue key =
            step.Parameters
            |> Array.tryFind (fun p -> String.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase))
            |> Option.map (fun p -> p.Value.Trim())

        let tryField (def: RowTypeDef) (name: string) =
            def.Fields
            |> Array.tryFind (fun f -> String.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))

        let lookupType (typeName: string) : RowTypeDef option =
            match TypeCatalog.tryGet catalog typeName with
            | Some def -> Some def
            | None ->
                synthesized |> Seq.tryFind (fun def -> String.Equals(def.Name, typeName, StringComparison.OrdinalIgnoreCase))

        let structurallyKnown (candidate: RowTypeDef) =
            // A hand-written structural twin (catalog) or an already-synthesized
            // twin is preferred over synthesis — the ADR's reuse rule.
            seq {
                yield! catalog.Definitions.Values
                yield! synthesized
            }
            |> Seq.tryFind (fun existing -> TypeCatalog.rowTypesStructurallyEqual existing candidate)

        let synthesize (candidate: RowTypeDef) (baseName: string) =
            // deterministic collision suffix: Name, Name2, Name3… (hash is the last resort, not needed yet)
            let mutable name = baseName
            let mutable index = 2

            while takenNames.Contains name do
                name <- $"{baseName}{index}"
                index <- index + 1

            takenNames.Add name |> ignore
            let named = { candidate with Name = name }
            synthesized.Add named
            name

        match TransformDeclarations.effectOf step.PluginId with
        | TransformTypeEffect.Identity -> Some currentType
        | TransformTypeEffect.Projection ->
            match lookupType currentType with
            | None ->
                diagnostics.Add(
                    diag "DFLOW012" $"transformer '{transformerId}': input type '{currentType}' of '{step.PluginId}' is not defined — cannot infer.")
                None
            | Some inputDef ->
                match paramValue "by" with
                | None ->
                    diagnostics.Add(
                        diag "DFLOW012" $"transformer '{transformerId}': '{step.PluginId}' requires a 'by' parameter to infer its projection.")
                    None
                | Some byColumn ->
                    match tryField inputDef byColumn with
                    | None ->
                        diagnostics.Add(
                            diag "DFLOW012"
                                $"transformer '{transformerId}': '{step.PluginId}' groups by '{byColumn}', which is not a field of '{currentType}'.")
                        None
                    | Some byField ->
                        // The SQL contract (GroupByStepSpec): SELECT by AS by, AGG(value) AS value.
                        let agg = paramValue "agg" |> Option.defaultValue "count"

                        let aggregateField =
                            if agg = "count" then
                                { Name = "value"; Type = DashType.Primitive DashPrimitive.Int; Optional = false }
                            else
                                match paramValue "value" with
                                | None ->
                                    { Name = "value"; Type = DashType.Primitive DashPrimitive.Int; Optional = false }
                                | Some valueColumn ->
                                    match tryField inputDef valueColumn with
                                    | None ->
                                        { Name = "value"; Type = DashType.Primitive DashPrimitive.Decimal; Optional = false }
                                    | Some valueField -> { Name = "value"; Type = valueField.Type; Optional = false }

                        let candidate =
                            { Name = ""
                              Fields = [| { byField with Name = byColumn }; aggregateField |] }

                        match structurallyKnown candidate with
                        | Some twin -> Some twin.Name
                        | None ->
                            let stem = stemOf currentType
                            Some(synthesize candidate (baseNameForEffect stem step))
        | TransformTypeEffect.Extend
        | TransformTypeEffect.Compose
        | TransformTypeEffect.RequireEqual ->
            // Not yet in the vocabulary (ADR-0126 roadmap: window/join/union). The
            // combinators exist in the contract model; until transforms declare
            // them they are unreachable here — treat as opaque so the chain asks
            // for an explicit output type.
            None
        | TransformTypeEffect.Opaque
        | _ -> None

    // --- composite source typing (ADR-0126 §1.1) -------------------------------

    /// <summary>Composite sources type from the constituent merge algebra:
    /// union — the sides must be structurally equal (fields of the left side);
    /// join — left fields plain, right fields colliding with left names qualify
    /// (<c>r.name</c>, the ADR's owner-qualification convention). A declared
    /// annotation is the verified contract (DFLOW013 on mismatch); an untyped
    /// composite synthesizes a deterministic name (<c>ModelRowJoinOrgRow</c>).</summary>
    let resolveCompositeSources
        (sources: DashflowSourceDef[])
        (catalog: TypeCatalog)
        : DashflowSourceDef[] * RowTypeDef list * InferenceDiagnostic list =
        let synthesized = ResizeArray<RowTypeDef>()
        let diagnostics = ResizeArray<InferenceDiagnostic>()
        let takenNames = HashSet<string>(StringComparer.OrdinalIgnoreCase)

        let fieldsOf (typeName: string) : RowTypeDef option =
            if String.IsNullOrWhiteSpace typeName then None
            else TypeCatalog.tryGet catalog typeName

        let sourceById (id: string) =
            sources |> Array.tryFind (fun s -> String.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase))

        let sideIds (origin: SourceOrigin) =
            match origin with
            | SourceOrigin.InnerJoin (l, r, _)
            | SourceOrigin.LeftJoin (l, r, _)
            | SourceOrigin.RightJoin (l, r, _)
            | SourceOrigin.FullJoin (l, r, _) -> Some(l, r, true)
            | SourceOrigin.UnionAll (l, r) -> Some(l, r, false)
            | SourceOrigin.Leaf _ -> None

        let patched =
            sources
            |> Array.map (fun source ->
                match sideIds source.Origin with
                | None -> source
                | Some (leftId, rightId, isJoin) ->
                    match sourceById leftId, sourceById rightId with
                    | Some left, Some right ->
                        match fieldsOf left.OutputRowType, fieldsOf right.OutputRowType with
                        | Some leftDef, Some rightDef ->
                            let mergedFields =
                                if isJoin then
                                    leftDef.Fields
                                    |> Array.append (
                                        rightDef.Fields
                                        |> Array.map (fun field ->
                                            let collides =
                                                leftDef.Fields
                                                |> Array.exists (fun lf ->
                                                    String.Equals(lf.Name, field.Name, StringComparison.OrdinalIgnoreCase))

                                            if collides then { field with Name = "r." + field.Name } else field))
                                else
                                    if not (TypeCatalog.rowTypesStructurallyEqual leftDef rightDef) then
                                        diagnostics.Add(
                                            diag "DFLOW013"
                                                $"source '{source.Id}': union sides '{left.OutputRowType}' and '{right.OutputRowType}' are not structurally equal.")

                                    leftDef.Fields

                            let candidate = { Name = ""; Fields = mergedFields }
                            let declared = source.OutputRowType

                            if not (String.IsNullOrWhiteSpace declared) then
                                match TypeCatalog.tryGet catalog declared with
                                | Some declaredDef
                                    when not (TypeCatalog.rowTypesStructurallyEqual declaredDef candidate) ->
                                    diagnostics.Add(
                                        diag "DFLOW013"
                                            $"source '{source.Id}': declared type '{declared}' does not match the composite merge of '{left.OutputRowType}' and '{right.OutputRowType}'.")
                                | _ -> ()

                                source
                            else
                                // A structural twin already in the catalog is preferred over
                                // synthesis (ADR-0128 §3 step 2): a second parse over the same
                                // flow reuses the first pass's name instead of minting a suffix.
                                let twin =
                                    Seq.append catalog.Definitions.Values synthesized
                                    |> Seq.tryFind (fun existing -> TypeCatalog.rowTypesStructurallyEqual existing candidate)

                                match twin with
                                | Some existing ->
                                    { source with OutputRowType = existing.Name }
                                | None ->
                                    let stemL = stemOf left.OutputRowType
                                    let stemR = stemOf right.OutputRowType
                                    let combiner = if isJoin then "Join" else "Union"
                                    let baseName = $"{stemL}{combiner}{stemR}Row"

                                    let mutable name = baseName
                                    let mutable index = 2

                                    while takenNames.Contains name do
                                        name <- $"{baseName}{index}"
                                        index <- index + 1

                                    takenNames.Add name |> ignore
                                    let named = { candidate with Name = name }
                                    synthesized.Add named
                                    { source with OutputRowType = name }
                        | _ ->
                            diagnostics.Add(
                                diag "DFLOW013"
                                    $"source '{source.Id}': composite side '{leftId}'/'{rightId}' has no row type — annotate the leaf source.")

                            source
                    | _ -> source)

        patched, List.ofSeq synthesized, List.ofSeq diagnostics

    // --- scope walk --------------------------------------------------------

    /// <summary>Infer port types for one flow scope (module or nested flow):
    /// sources seed explicit types, links propagate them through transformer
    /// steps. Transformers that declared output types keep them (verified);
    /// untyped outputs receive inferred names.</summary>
    let inferScope
        (sources: DashflowSourceDef[])
        (transformers: DashflowTransformerDef[])
        (links: FlowLinkDef[])
        (catalog: TypeCatalog)
        : InferenceOutcome =
        let outputTypes = Dictionary<string * string, string>()
        let synthesized = ResizeArray<RowTypeDef>()
        let diagnostics = ResizeArray<InferenceDiagnostic>()
        let takenNames = HashSet<string>(StringComparer.OrdinalIgnoreCase)

        let sourceRowType (nodeId: string) =
            sources
            |> Array.tryFind (fun s -> String.Equals(s.Id, nodeId, StringComparison.OrdinalIgnoreCase))
            |> Option.map (fun s -> s.OutputRowType)

        let transformerById (nodeId: string) =
            transformers
            |> Array.tryFind (fun t -> String.Equals(t.Id, nodeId, StringComparison.OrdinalIgnoreCase))

        // The resolved output type of any producer node (source or already-inferred transformer).
        let rec producerType (nodeId: string) =
            sourceRowType nodeId
            |> Option.orElseWith (fun () ->
                match transformerById nodeId with
                | None -> None
                | Some transformer ->
                    match transformer.Outputs with
                    | [| (name, declared) |] ->
                        if String.IsNullOrWhiteSpace declared then
                            match outputTypes.TryGetValue ((nodeId, name)) with
                            | true, inferred -> Some inferred
                            | false, _ -> None
                        else
                            Some declared
                    | _ -> None)

        let inputTypeOf (transformer: DashflowTransformerDef) =
            links
            |> Array.tryFind (fun link -> String.Equals(link.ToNode, transformer.Id, StringComparison.OrdinalIgnoreCase))
            |> Option.bind (fun link -> producerType link.FromNode)

        let explicitOutput (transformer: DashflowTransformerDef) =
            match transformer.Outputs with
            | [| (name, declared) |] when not (String.IsNullOrWhiteSpace declared) -> Some(name, declared)
            | _ -> None

        // Topological order: keep resolving transformers whose input producer is
        // known; a leftover cycle/unresolvable chain is left to the graph typecheck.
        let pending = ResizeArray(transformers)
        let mutable progress = true

        while progress && pending.Count > 0 do
            progress <- false
            let settled = ResizeArray()

            for transformer in pending do
                match inputTypeOf transformer with
                | None -> ()
                | Some inputType ->
                    settled.Add transformer
                    progress <- true
                    // fold the steps
                    let mutable current = Some inputType

                    for step in transformer.Steps do
                        match current with
                        | Some typeName ->
                            current <-
                                applyStep catalog synthesized takenNames diagnostics transformer.Id typeName step
                        | None -> ()

                    let inputStem = stemOf inputType

                    let inferredName =
                        match current with
                        | Some name -> name
                        | None ->
                            // inference stopped (opaque step or diagnostics) — the chain needs explicit typing
                            if explicitOutput transformer |> Option.isNone then
                                diagnostics.Add(
                                    diag "DFLOW011"
                                        $"transformer '{transformer.Id}': inference stopped at an opaque step — declare the output port type (output stream rows: MyType).")
                            inputStem + "OutRow"

                    match explicitOutput transformer with
                    | Some (portName, declared) ->
                        // transform-typecheck: the declared type is a verified contract
                        let declaredDef = TypeCatalog.tryGet catalog declared

                        let inferredDef =
                            synthesized
                            |> Seq.tryFind (fun d -> d.Name = inferredName)
                            |> Option.orElseWith (fun () -> TypeCatalog.tryGet catalog inferredName)

                        match declaredDef, inferredDef with
                        | Some declaredType, Some inferredType
                            when not (TypeCatalog.rowTypesStructurallyEqual declaredType inferredType) ->
                            diagnostics.Add(
                                diag "DFLOW010"
                                    $"transformer '{transformer.Id}': declared output type '{declared}' does not match the inferred '{inferredType.Name}'.")
                        | _ -> ()

                        outputTypes.[(transformer.Id, portName)] <- declared
                    | None ->
                        let portName =
                            match transformer.Outputs with
                            | [| (name, _) |] -> name
                            | _ -> "rows"

                        outputTypes.[(transformer.Id, portName)] <- inferredName

            for done' in settled do
                pending.Remove done' |> ignore

        { OutputTypes = outputTypes :> IReadOnlyDictionary<_, _>
          Synthesized = List.ofSeq synthesized
          Diagnostics = List.ofSeq diagnostics }
