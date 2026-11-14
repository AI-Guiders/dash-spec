namespace DashSpec.Modeling.Parse

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Syntax

/// <summary>
/// Multi-form construct declaration (ADR-0106): one vocabulary, three surface forms
/// (<c>TableForm</c>, <c>BlockForm</c>, <c>LineForm</c>), one semantics.
/// Every surface form normalizes to the same attribute map and converges on
/// <c>CellsToRow</c> → <c>ConstructRow</c>.
/// </summary>
module Construct =

    /// Surface representation of a construct instance or table of instances.
    [<RequireQualifiedAccess>]
    type ConstructForm =
        /// Tabular surface: array of cell rows (first row = header columns, subsequent rows = data cells).
        | TableForm of rows: string[][]
        /// Block surface: header bindings (e.g. `id`, `label`) + body `key = value` properties.
        | BlockForm of headerBindings: (string * string) seq * properties: seq<KeyValuePair<string, string>>
        /// Dotted property surface (`<id>.<prop> = <value>` or `<kind>.<id>.<prop> = <value>`), grouped by object id.
        | DottedForm of instances: (string * (string * string) list) list
        /// Single-line / inline surface: positional or inline `(key, value)` bindings.
        | LineForm of bindings: (string * string) seq

    /// A declared construct: the surface forms' equivalence, stated once in code.
    type Declaration<'Row> =
        { /// Construct name (error messages: "{name} table …").
          Name: string
          /// Allowed attribute/column names — validated once, shared by all forms.
          Vocabulary: string list
          /// Attribute map (from TableForm, BlockForm, or LineForm) → domain row.
          CellsToRow: Map<string, string> -> 'Row
          /// One semantics path: a domain row (from any form) → the model.
          ConstructRow: 'Row -> unit }

    let private isUnset (cell: string) =
        String.IsNullOrWhiteSpace cell || cell = "-"

    /// Strip matching outer double or single quotes from a cell or property literal.
    let unquote (raw: string) : string =
        if isNull raw then
            ""
        else
            let t = raw.Trim()
            if t.Length >= 2 && ((t.[0] = '"' && t.[t.Length - 1] = '"') || (t.[0] = '\'' && t.[t.Length - 1] = '\'')) then
                t.Substring(1, t.Length - 2)
            else
                t

    /// Try to parse a boolean cell/property value (`true`/`yes`/`1` or `false`/`no`/`0`).
    let tryParseBool (raw: string option) : bool option =
        match raw with
        | Some s when not (String.IsNullOrWhiteSpace s) ->
            match (unquote s).Trim().ToLowerInvariant() with
            | "true" | "yes" | "1" -> Some true
            | "false" | "no" | "0" -> Some false
            | _ -> None
        | _ -> None

    /// Try to parse an integer cell/property value.
    let tryParseInt (raw: string option) : int option =
        match raw with
        | Some s when not (String.IsNullOrWhiteSpace s) ->
            match Int32.TryParse((unquote s).Trim()) with
            | true, v -> Some v
            | _ -> None
        | _ -> None

    /// Optional cell: `-`/empty = unset (ADR-0104 family semantics).
    let tryCell (m: Map<string, string>) (name: string) : string option =
        match m |> Map.tryPick (fun k v -> if String.Equals(k, name, StringComparison.OrdinalIgnoreCase) then Some v else None) with
        | Some v when not (isUnset v) -> Some(v.Trim())
        | _ -> None

    /// Required cell with the row id for the error message.
    let requireCell (m: Map<string, string>) (declName: string) (rowId: string) (name: string) : string =
        match tryCell m name with
        | Some v -> v
        | None ->
            raise (DashSpecMissingMemberException(name, $"{declName} table row '{rowId}' requires a '{name}' cell."))

    let private bindingsToMap (pairs: (string * string) seq) : Map<string, string> =
        let mutable m = Map.empty<string, string>

        for k, v in pairs do
            if not (String.IsNullOrWhiteSpace k) && not (isUnset v) then
                m <- m |> Map.add (k.Trim().ToLowerInvariant()) (v.Trim())

        m

    let private tableCellsToMaps (declName: string) (rows: string[][]) : Map<string, string> list =
        match rows |> Array.toList with
        | header :: rest ->
            let keys = header |> Array.map (fun h -> h.Trim().ToLowerInvariant())

            let dataRows =
                rest
                |> List.filter (fun cells -> not (TableLiteral.isSeparator cells))

            if dataRows.IsEmpty then
                raise (DashSpecTableStructureException(declName, $"{declName} table requires at least one row after the header."))

            dataRows
            |> List.map (fun cells ->
                [ for i in 0 .. (min (cells.Length - 1) (keys.Length - 1)) do
                    if keys.[i].Length > 0 then
                        yield keys.[i], cells.[i].Trim() ]
                |> Map.ofList)
        | [] ->
            raise (DashSpecTableStructureException(declName, $"{declName} table requires at least one row after the header."))

    let private validateVocabulary (decl: Declaration<'Row>) (maps: Map<string, string> list) =
        let allowed = HashSet<string>(decl.Vocabulary, StringComparer.OrdinalIgnoreCase)

        for m in maps do
            for k in m.Keys do
                if not (allowed.Contains k) then
                    let vocabularyList = String.Join(", ", decl.Vocabulary)

                    raise (
                        DashSpecUnknownPropertyException(
                            k,
                            $"{decl.Name} table attribute '{k}' is not one of: {vocabularyList}.")
                    )

    /// Normalize any <see cref="ConstructForm"/> into validated attribute maps and apply <see cref="Declaration.CellsToRow"/> + <see cref="Declaration.ConstructRow"/>.
    let apply (decl: Declaration<'Row>) (form: ConstructForm) : unit =
        let maps =
            match form with
            | ConstructForm.TableForm rows ->
                tableCellsToMaps decl.Name rows
            | ConstructForm.BlockForm(headerBindings, properties) ->
                let combined =
                    seq {
                        yield! headerBindings
                        for kv in properties do
                            yield kv.Key, kv.Value
                    }

                [ bindingsToMap combined ]
            | ConstructForm.DottedForm instances ->
                instances
                |> List.map (fun (idAttr, pairs) ->
                    bindingsToMap pairs |> fun m -> if String.IsNullOrWhiteSpace idAttr then m else m)
            | ConstructForm.LineForm bindings ->
                [ bindingsToMap bindings ]

        validateVocabulary decl maps

        for m in maps do
            decl.CellsToRow m |> decl.ConstructRow

    /// Extract <see cref="ConstructForm.BlockForm"/> directly from an AST <see cref="BlockDeclarationSyntax"/>.
    let fromAstBlock (headerBindings: (string * string) seq) (block: BlockDeclarationSyntax) : ConstructForm =
        let props =
            block.Members
            |> Array.choose (function
                | DashSpecAstNode.PropertyAssignment p -> Some(KeyValuePair(p.Key, p.ValueText))
                | _ -> None)

        ConstructForm.BlockForm(headerBindings, props)

    /// Extract <see cref="ConstructForm.TableForm"/> from AST members if any <see cref="DashSpecAstNode.TableRowDeclaration"/> is present.
    let tryFromAstTable (members: DashSpecAstNode[]) : ConstructForm option =
        let rows =
            members
            |> Array.choose (function
                | DashSpecAstNode.TableRowDeclaration r -> Some r.Cells
                | _ -> None)

        if rows.Length = 0 then None else Some(ConstructForm.TableForm rows)

    /// Extract <see cref="ConstructForm.DottedForm"/> from dotted <see cref="DashSpecAstNode.PropertyAssignment"/>
    /// nodes (`<id>.<prop> = <value>` or `<kind>.<id>.<prop> = <value>`), grouping all properties of the same
    /// object id in declaration order into one construct row.
    let tryFromAstDotted
        (decl: Declaration<'Row>)
        (kindPrefix: string option)
        (idAttribute: string)
        (members: DashSpecAstNode[])
        : ConstructForm option
        =
        let allowed = HashSet<string>(decl.Vocabulary, StringComparer.OrdinalIgnoreCase)
        let order = ResizeArray<string>()
        let byId = Dictionary<string, ResizeArray<string * string>>(StringComparer.OrdinalIgnoreCase)

        let addProp (objId: string) (propName: string) (valText: string) =
            if not (byId.ContainsKey objId) then
                order.Add objId
                byId.[objId] <- ResizeArray<string * string>()
                if not (String.IsNullOrWhiteSpace idAttribute) then
                    byId.[objId].Add(idAttribute, objId)
            byId.[objId].Add(propName, valText)

        for m in members do
            match m with
            | DashSpecAstNode.PropertyAssignment p when p.KeyPath.IsQualified ->
                match p.KeyPath.Segments with
                | [| objId; propName |] when allowed.Contains propName ->
                    addProp objId propName p.ValueText
                | [| prefix; objId; propName |] when allowed.Contains propName ->
                    match kindPrefix with
                    | Some kp when String.Equals(prefix, kp, StringComparison.OrdinalIgnoreCase) ->
                        addProp objId propName p.ValueText
                    | None when String.Equals(prefix, decl.Name, StringComparison.OrdinalIgnoreCase) ->
                        addProp objId propName p.ValueText
                    | None when (LanguageObjectKind.tryParse prefix).IsSome ->
                        addProp $"{prefix}.{objId}" propName p.ValueText
                    | _ -> ()
                | _ -> ()
            | _ -> ()

        if order.Count = 0 then
            None
        else
            let instances =
                [ for objId in order -> objId, (byId.[objId] |> Seq.toList) ]
            Some(ConstructForm.DottedForm instances)

    /// Unified AST construct projection: applies <paramref name="decl"/> across
    /// <see cref="ConstructForm.TableForm"/> (`| col1 | col2 |`),
    /// <see cref="ConstructForm.BlockForm"/> (`<blockKeyword> [<id>] ... end <blockKeyword>`),
    /// and <see cref="ConstructForm.DottedForm"/> (`<id>.<prop> = <value>`) in a single call.
    let projectAst
        (decl: Declaration<'Row>)
        (blockKeyword: DashSpecBlockKeyword option)
        (idAttribute: string)
        (members: DashSpecAstNode[])
        : unit
        =
        match tryFromAstTable members with
        | Some tableForm -> apply decl tableForm
        | None -> ()

        match blockKeyword with
        | None -> ()
        | Some kw ->
            for m in members do
                match m with
                | DashSpecAstNode.BlockDeclaration b ->
                    match b.Opener with
                    | DashSpecBlockOpener.Named(bkw, itemId) when bkw = kw ->
                        let header =
                            if String.IsNullOrWhiteSpace idAttribute then [] else [ idAttribute, itemId ]
                        apply decl (fromAstBlock header b)
                    | DashSpecBlockOpener.Anonymous bkw when bkw = kw ->
                        apply decl (fromAstBlock [] b)
                    | _ -> ()
                | _ -> ()

        let kindPrefix = blockKeyword |> Option.map DashSpecBlockKeyword.toEndName
        match tryFromAstDotted decl kindPrefix idAttribute members with
        | Some dottedForm -> apply decl dottedForm
        | None -> ()

    /// Convert raw pipe-table lines into <see cref="ConstructForm.TableForm"/> and apply the declaration.
    let applyTableRows (decl: Declaration<'Row>) (rows: string list) : unit =
        let cellRows = rows |> List.map TableLiteral.splitCells |> List.toArray
        apply decl (ConstructForm.TableForm cellRows)

    /// Streaming consumer that converts each pipe-table row immediately into its typed domain object <c>'Row</c>
    /// without buffering raw strings in the caller.
    type TableConsumer =
        { ConsumeRow: string -> unit
          Complete: unit -> unit }

    let createTableConsumer (decl: Declaration<'Row>) : TableConsumer =
        let cellRows = ResizeArray<string[]>()

        let consumeRow (rawRow: string) =
            cellRows.Add(TableLiteral.splitCells rawRow)

        let complete () =
            if cellRows.Count > 0 then
                apply decl (ConstructForm.TableForm(cellRows.ToArray()))

        { ConsumeRow = consumeRow
          Complete = complete }
