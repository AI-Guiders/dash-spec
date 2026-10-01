namespace DashSpec.Modeling.Parse.Diagram

open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

/// Table diagram — `formats` child block via BlockGrammar (ADR-0070).
module TableDiagramParser =

    let parse (reader: TokenReader) =
        let schema = DiagramKindRegistry.getProperties "table"
        BlockGrammar.parseKeywordContainer
            reader
            "table"
            "diagram table"
            [ BlockGrammar.ChildKeywordMerge("formats", "formats", "diagram formats", MemberGrammar.parsePresetRestOfLine, BlockGrammar.tableFormatsMerge)
              BlockGrammar.SchemaProperties(schema, false, false) ]
            None
