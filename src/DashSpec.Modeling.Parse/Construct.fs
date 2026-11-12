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
            raise (DashSpecParseException($"{declName} table row '{rowId}' requires a '{name}' cell."))

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
                raise (DashSpecParseException($"{declName} table requires at least one row after the header."))

            dataRows
            |> List.map (fun cells ->
                [ for i in 0 .. (min (cells.Length - 1) (keys.Length - 1)) do
                    if keys.[i].Length > 0 then
                        yield keys.[i], cells.[i].Trim() ]
                |> Map.ofList)
        | [] ->
            raise (DashSpecParseException($"{declName} table requires at least one row after the header."))

    let private validateVocabulary (decl: Declaration<'Row>) (maps: Map<string, string> list) =
        let allowed = HashSet<string>(decl.Vocabulary, StringComparer.OrdinalIgnoreCase)

        for m in maps do
            for k in m.Keys do
                if not (allowed.Contains k) then
                    let vocabularyList = String.Join(", ", decl.Vocabulary)

                    raise (
                        DashSpecParseException(
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
