namespace DashSpec.Modeling.Parse.Diagram

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

/// Table diagram — `formats` child block via BlockGrammar (ADR-0070) plus
/// `columns` as flat comma-list or GDL-style pipe table
/// (<c>| id | format | label | sorting |</c>; ADR-0004 deferred step, ADR-0057 GDL-tables).
/// ADR-0106: the `columns` table side rides the construct declaration — vocabulary,
/// header/unset semantics and the row loop are kernel-shared.
module TableDiagramParser =

    /// Desugars the `columns` pipe table into IR properties:
    /// `columns` (ids in row order), `column_formats`, `column_labels` (newline map), `order_by` (sorting cells in row order).
    let private applyColumnsTable (rows: string list) (values: Dictionary<string, string>) =
        if rows.IsEmpty then
            ()
        else
            let ids = ResizeArray<string>()
            let formats = Dictionary<string, string>()
            let labels = Dictionary<string, string>()
            let sorting = ResizeArray<string>()
            let alignments = Dictionary<string, string>()
            let headerAlignments = Dictionary<string, string>()

            let validateAlignment (raw: string) (column: string) =
                match raw.Trim().ToLowerInvariant() with
                | "left" | "center" | "right" | "justify" as ok -> ok
                | other ->
                    raise (
                        DashSpecParseException(
                            $"diagram columns table alignment for '{column}' must be left, center, right, or justify; got '{other}'.")
                    )

            let cellsToColumn (m: Map<string, string>) =
                match Construct.tryCell m "id" with
                | Some id ->
                    if ids.Contains id then
                        raise (DashSpecParseException($"diagram columns table declares '{id}' more than once."))

                    ids.Add id

                    (match Construct.tryCell m "format" with
                     | Some f -> formats.[id] <- f
                     | None -> ())

                    (match Construct.tryCell m "label" with
                     | Some l -> labels.[id] <- l
                     | None -> ())

                    (match Construct.tryCell m "alignment" with
                     | Some a -> alignments.[id] <- validateAlignment a id
                     | None -> ())

                    (match Construct.tryCell m "header_alignment" with
                     | Some a -> headerAlignments.[id] <- validateAlignment a id
                     | None -> ())

                    (match Construct.tryCell m "sorting" with
                     | Some s ->
                         (match s.ToUpperInvariant() with
                          | "ASC"
                          | "DESC" as dir -> sorting.Add($"{id} {dir}")
                          | other -> raise (DashSpecParseException($"diagram columns table sorting for '{id}' must be ASC or DESC, got '{other}'.")))
                     | None -> ())
                | None -> raise (DashSpecParseException("diagram columns table row requires a non-empty 'id' cell."))

            let declaration : Construct.Declaration<unit> =
                { Name = "diagram columns"
                  Vocabulary = [ "id"; "format"; "label"; "sorting"; "alignment"; "header_alignment" ]
                  CellsToRow = cellsToColumn
                  ConstructRow = ignore }

            Construct.applyTableRows declaration rows

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

            if alignments.Count > 0 then
                values.["column_alignments"] <- BlockGrammar.serializeColumnFormatMap alignments

            if headerAlignments.Count > 0 then
                values.["column_header_alignments"] <- BlockGrammar.serializeColumnFormatMap headerAlignments

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
