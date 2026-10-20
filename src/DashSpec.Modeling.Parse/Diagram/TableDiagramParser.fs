namespace DashSpec.Modeling.Parse.Diagram

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

/// Table diagram — `formats` child block via BlockGrammar (ADR-0070) plus
/// `columns` as flat comma-list or GDL-style pipe table
/// (<c>| id | format | label | sorting |</c>; ADR-0004 deferred step, ADR-0057 GDL-tables).
module TableDiagramParser =

    let private allowedAttributes =
        HashSet<string>([ "id"; "format"; "label"; "sorting" ], StringComparer.OrdinalIgnoreCase)

    let private isUnset (cell: string) =
        String.IsNullOrWhiteSpace cell || cell = "-"

    /// Desugars the `columns` pipe table into IR properties:
    /// `columns` (ids in row order), `column_formats`, `column_labels` (newline map), `order_by` (sorting cells in row order).
    let private applyColumnsTable (rows: string list) (values: Dictionary<string, string>) =
        if rows.IsEmpty then
            ()
        else
            let maps = TableLiteral.parseRows rows

            if maps.IsEmpty then
                raise (DashSpecParseException("diagram columns table requires at least one column row after the header."))

            let attrs = HashSet<string>()
            for m in maps do
                for k in m.Keys do
                    attrs.Add k |> ignore

            for a in attrs do
                if not (allowedAttributes.Contains a) then
                    raise (DashSpecParseException($"diagram columns table attribute '{a}' is not one of: id, format, label, sorting."))

            let ids = ResizeArray<string>()
            let formats = Dictionary<string, string>()
            let labels = Dictionary<string, string>()
            let sorting = ResizeArray<string>()

            for m in maps do
                match m.TryFind "id" with
                | Some id when not (isUnset id) ->
                    if ids.Contains id then
                        raise (DashSpecParseException($"diagram columns table declares '{id}' more than once."))

                    ids.Add id

                    (match m.TryFind "format" with
                     | Some f when not (isUnset f) -> formats.[id] <- f.Trim()
                     | _ -> ())

                    (match m.TryFind "label" with
                     | Some l when not (isUnset l) -> labels.[id] <- l.Trim()
                     | _ -> ())

                    (match m.TryFind "sorting" with
                     | Some s when not (isUnset s) ->
                         (match s.Trim().ToUpperInvariant() with
                          | "ASC"
                          | "DESC" as dir -> sorting.Add($"{id} {dir}")
                          | other -> raise (DashSpecParseException($"diagram columns table sorting for '{id}' must be ASC or DESC, got '{other}'.")))
                     | _ -> ())
                | _ -> raise (DashSpecParseException("diagram columns table row requires a non-empty 'id' cell."))

            if values.ContainsKey "columns" then
                raise (DashSpecParseException("diagram columns is declared both as a flat list and as a table; pick one."))

            values.["columns"] <- String.Join(", ", ids)

            if formats.Count > 0 then
                if values.ContainsKey "column_formats" then
                    raise (DashSpecParseException("column format is declared both in a formats block and in the columns table; pick one."))

                values.["column_formats"] <- BlockGrammar.serializeColumnFormatMap formats

            if labels.Count > 0 then
                values.["column_labels"] <- BlockGrammar.serializeColumnLabelMap labels

            if sorting.Count > 0 then
                if values.ContainsKey "order_by" then
                    raise (DashSpecParseException("diagram order_by is declared both as a flat property and as sorting in the columns table; pick one."))

                values.["order_by"] <- String.Join(", ", sorting)

    let parse (reader: TokenReader) =
        let schema = DiagramKindRegistry.getProperties "table"
        let pipeRows = ResizeArray<string>()

        let values =
            BlockGrammar.parseKeywordContainer
                reader
                "table"
                "diagram table"
                [ BlockGrammar.ChildKeywordMerge("formats", "formats", "diagram formats", MemberGrammar.parsePresetRestOfLine, BlockGrammar.tableFormatsMerge)
                  BlockGrammar.KeywordScalarOrBlock(
                      "columns",
                      "columns",
                      "diagram columns",
                      (fun r values ->
                          r.Expect TokenKind.Eq
                          values.["columns"] <- r.ReadCommaSeparatedValues()),
                      (fun r _ ->
                          pipeRows.AddRange(BlockGrammar.readPipeTableRows r "columns" "diagram columns")))
                  BlockGrammar.SchemaProperties(schema, false, false) ]
                None

        applyColumnsTable (List.ofSeq pipeRows) values
        values
