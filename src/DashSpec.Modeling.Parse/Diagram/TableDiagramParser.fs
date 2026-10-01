namespace DashSpec.Modeling.Parse.Diagram

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

/// Table diagram — `formats` child block via BlockGrammar (ADR-0070).
module TableDiagramParser =

    let private legacyInlineColumnFormats =
        BlockGrammar.KeywordScalarOrBlock(
            "column_formats",
            "formats",
            "diagram formats",
            (fun reader values ->
                reader.Expect TokenKind.Eq
                let line = reader.ReadRestOfLine().Trim()
                if String.IsNullOrWhiteSpace line then
                    raise (DashSpecParseException("diagram table: column_formats value is required."))
                values.["column_formats"] <- line),
            (fun reader values ->
                let map =
                    MemberGrammar.parseStringMapBlock reader "formats" "diagram formats" MemberGrammar.parsePresetRestOfLine
                values.["column_formats"] <- BlockGrammar.tableFormatsMerge.Serialize map))

    let parse (reader: TokenReader) =
        let schema = DiagramKindRegistry.getProperties "table"
        BlockGrammar.parseKeywordContainer
            reader
            "table"
            "diagram table"
            [ BlockGrammar.ChildKeywordMerge("formats", "formats", "diagram formats", MemberGrammar.parsePresetRestOfLine, BlockGrammar.tableFormatsMerge)
              legacyInlineColumnFormats
              BlockGrammar.SchemaProperties(schema, false, false) ]
            None
