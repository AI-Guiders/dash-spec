namespace DashSpec.Modeling.Parse.DataFlow

open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

/// Graph port endpoints (`from node.port`) — surface Accessor syntax, IR FlowNodePortRef on FlowGraph edges.
module FlowPortRefParser =

    /// `from` + producer output on the flow graph (exactly `nodeId`.`outputPort`).
    let readProducerAfterFrom (reader: TokenReader) =
        reader.ExpectKeyword "from"

        match AccessorGrammar.read reader with
        | AccessorGrammar.Accessor.Select (AccessorGrammar.Accessor.Name nodeId, portName) ->
            FlowNodePortRef.producer nodeId portName
        | accessor ->
            raise (
                DashSpecParseException(
                    $"flow input requires a producer port reference node.port after from, got '{accessor.Dotted}'."
                )
            )
