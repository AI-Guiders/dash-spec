namespace DashSpec.Modeling.Parse.Card

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Lexing

module CardFlowInputParser =

    let parse (reader: TokenReader) (cardId: string) =
        reader.SkipNewlines()
        let alias = reader.ReadIdent()

        if String.IsNullOrWhiteSpace alias then
            raise (DashSpecParseException($"Card '{cardId}': input requires an alias name."))

        if not (reader.TryKeyword "from") then
            raise (DashSpecParseException($"Card '{cardId}': input '{alias}' requires 'from node.port'."))

        let nodeId = reader.ReadIdent()

        if String.IsNullOrWhiteSpace nodeId then
            raise (DashSpecParseException($"Card '{cardId}': input '{alias}' requires a flow node id before '.'."))

        reader.Expect TokenKind.Dot
        let portName = reader.ReadIdent()

        if String.IsNullOrWhiteSpace portName then
            raise (DashSpecParseException($"Card '{cardId}': input '{alias}' requires a port name after '.'."))

        { Alias = alias
          NodeId = nodeId
          PortName = portName }
