namespace DashSpec.Modeling.Parse

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Lexing

/// <summary>
/// Multi-form construct declaration (ADR-0106): one vocabulary, two surfaces, one semantics.
/// Table form: header row declares columns, data rows carry cells (TableLiteral semantics).
/// Block form: one construct unit per nested block/statement group — the construct's own projection.
/// Both converge on <c>ConstructRow</c> — the single semantics path.
/// </summary>
module Construct =

    /// A declared construct: the two forms' equivalence, stated once in code.
    type Declaration<'Row> =
        { /// Construct name (error messages: "{name} table …").
          Name: string
          /// Allowed column names — validated once, shared by both forms.
          Vocabulary: string list
          /// Table form: one data-row map → a construct row.
          CellsToRow: Map<string, string> -> 'Row
          /// One semantics path: a row (from either form) → the model.
          ConstructRow: 'Row -> unit }

    let private isUnset (cell: string) =
        String.IsNullOrWhiteSpace cell || cell = "-"

    /// Optional cell: `-`/empty = unset (ADR-0104 family semantics).
    let tryCell (m: Map<string, string>) (name: string) : string option =
        match m.TryFind name with
        | Some v when not (isUnset v) -> Some(v.Trim())
        | _ -> None

    /// Required cell with the row id for the error message.
    let requireCell (m: Map<string, string>) (declName: string) (rowId: string) (name: string) : string =
        match tryCell m name with
        | Some v -> v
        | None ->
            raise (DashSpecParseException($"{declName} table row '{rowId}' requires a '{name}' cell."))

    /// <summary>
    /// Table rows (collected via <c>BlockGrammar.TableRowRows</c>) through the declaration.
    /// Kernel-shared: header row declares columns, separator rows skipped, vocabulary validated,
    /// one construction path per data row.
    /// </summary>
    let applyTableRows (decl: Declaration<'Row>) (rows: string list) : unit =
        let maps = TableLiteral.parseRows rows

        if maps.IsEmpty then
            raise (DashSpecParseException($"{decl.Name} table requires at least one row after the header."))

        let attrs = HashSet<string>(StringComparer.OrdinalIgnoreCase)

        for m in maps do
            for k in m.Keys do
                attrs.Add k |> ignore

        let allowed = HashSet<string>(decl.Vocabulary, StringComparer.OrdinalIgnoreCase)

        for a in attrs do
            if not (allowed.Contains a) then
                let vocabularyList = String.Join(", ", decl.Vocabulary)
                raise (
                    DashSpecParseException(
                        $"{decl.Name} table attribute '{a}' is not one of: {vocabularyList}.")
                )

        for m in maps do
            decl.CellsToRow m |> decl.ConstructRow
