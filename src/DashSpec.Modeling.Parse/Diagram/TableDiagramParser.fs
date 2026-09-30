namespace DashSpec.Modeling.Parse.Diagram

open DashSpec.Modeling.Parse

/// Table diagram — `formats` child block via BlockGrammar (ADR-0070).
module TableDiagramParser =

    let parse (reader: Lexing.TokenReader) =
        let schema = DiagramKindRegistry.getProperties "table"
        BlockGrammar.parseKeywordContainer
            reader
            "table"
            "diagram table"
            [ BlockGrammar.ChildKeywordMerge("formats", "formats", "diagram formats", BlockGrammar.tableFormatsMerge)
              BlockGrammar.SchemaProperties(schema, false, false) ]
