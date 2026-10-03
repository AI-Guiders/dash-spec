namespace DashSpec.Modeling.Parse.DataFlow

open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

/// Legacy inline producer refs (`from node.port`) — prefer `FlowLinkParser` in `dataflow` blocks.
module FlowPortRefParser =

    let readProducerRef (reader: TokenReader) =
        match AccessorGrammar.read reader with
        | AccessorGrammar.Accessor.Select (AccessorGrammar.Accessor.Name nodeId, portName) ->
            FlowNodePortRef.producer nodeId portName
        | accessor ->
            raise (
                DashSpecParseException(
                    $"flow producer reference requires node.port, got '{accessor.Dotted}'."
                )
            )

    let readProducerAfterFrom (reader: TokenReader) =
        reader.ExpectKeyword "from"
        readProducerRef reader
