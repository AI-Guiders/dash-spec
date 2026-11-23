namespace DashSpec.Modeling.Parse.Diagram

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Syntax

/// Table diagram — `formats` child block via BlockGrammar (ADR-0070) plus
/// `columns` as flat comma-list or GDL-style pipe table
/// (<c>| id | format | label | size | align | sorting |</c>; ADR-0004 deferred step, ADR-0057 GDL-tables).
/// ADR-0106: the `columns` table side rides the construct declaration — vocabulary,
/// header/unset semantics and the row loop are kernel-shared.
module TableDiagramParser =

    /// Projects the `columns` pipe table into typed `ColumnRowSyntax` records and IR properties.
    let private applyColumnsTable (rows: string list) (values: Dictionary<string, string>) : IReadOnlyList<ColumnRowSyntax> =
        if rows.IsEmpty then
            [||] :> IReadOnlyList<ColumnRowSyntax>
        else
            let ids = ResizeArray<string>()
            let typedColumns = ResizeArray<ColumnRowSyntax>()
            let formats = Dictionary<string, string>()
            let labels = Dictionary<string, string>()
            let sorting = ResizeArray<string>()
            let alignments = Dictionary<string, string>()
            let headerAlignments = Dictionary<string, string>()

            let cellsToColumn (m: Map<string, string>) =
                match Construct.tryCell m "id" with
                | Some id ->
                    if ids.Contains id then
                        raise (DashSpecParseException($"diagram columns table declares '{id}' more than once."))

                    ids.Add id

                    let fmt = Construct.tryCell m "format"
                    match fmt with
                    | Some f -> formats.[id] <- f
                    | None -> ()

                    let lbl = Construct.tryCell m "label"
                    match lbl with
                    | Some l -> labels.[id] <- l
                    | None -> ()

                    let colSize =
                        Construct.tryCell m "size"
                        |> Option.map (TableColumnSize.parse id)

                    let colAlign =
                        Construct.tryCell m "align"
                        |> Option.orElseWith (fun () -> Construct.tryCell m "alignment")
                        |> Option.map (TableColumnAlign.parse id)

                    match colAlign with
                    | Some a -> alignments.[id] <- a.WireName
                    | None -> ()

                    let hdrAlign =
                        Construct.tryCell m "header_align"
                        |> Option.orElseWith (fun () -> Construct.tryCell m "header_alignment")
                        |> Option.map (TableColumnAlign.parse id)

                    match hdrAlign with
                    | Some a -> headerAlignments.[id] <- a.WireName
                    | None -> ()

                    let sortDir =
                        Construct.tryCell m "sorting"
                        |> Option.map (TableSortDirection.parse id)

                    match sortDir with
                    | Some d -> sorting.Add($"{id} {d.WireName}")
                    | None -> ()

                    typedColumns.Add
                        { Id = id
                          Format = fmt
                          Label = lbl
                          Size = colSize
                          Align = colAlign
                          HeaderAlign = hdrAlign
                          Sorting = sortDir
                          Alignment = colAlign |> Option.map (fun a -> a.WireName)
                          HeaderAlignment = hdrAlign |> Option.map (fun a -> a.WireName) }
                | None -> raise (DashSpecParseException("diagram columns table row requires a non-empty 'id' cell."))

            let declaration : Construct.Declaration<unit> =
                { Name = "diagram columns"
                  Vocabulary = [ "id"; "format"; "label"; "size"; "align"; "alignment"; "header_align"; "header_alignment"; "sorting" ]
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
                if values.ContainsKey "sort" then
                    raise (DashSpecParseException("diagram sort is declared both as a flat property and as sorting in the columns table; pick one."))

                values.["sort"] <- String.Join(", ", sorting)

            if alignments.Count > 0 then
                values.["column_alignments"] <- BlockGrammar.serializeColumnFormatMap alignments

            if headerAlignments.Count > 0 then
                values.["column_header_alignments"] <- BlockGrammar.serializeColumnFormatMap headerAlignments

            typedColumns :> IReadOnlyList<ColumnRowSyntax>

    let parseWithColumns (reader: TokenReader) : Dictionary<string, string> * IReadOnlyList<ColumnRowSyntax> =
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

        let cols = applyColumnsTable (List.ofSeq pipeRows) values
        values, cols

    let parse (reader: TokenReader) =
        parseWithColumns reader |> fst
