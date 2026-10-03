namespace DashSpec.Modeling.Parse.DataFlow

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Lexing

/// PlantUML-style flow links inside a `dataflow` block (`a --> b` optional input port name).
module FlowLinkParser =

    let tryParseLink (reader: TokenReader) (fromNode: string) =
        if not (reader.IsAt TokenKind.FlowArrow) then
            None
        else
            reader.Advance()
            let toNode = reader.ReadIdent()

            if String.IsNullOrWhiteSpace toNode then
                raise (DashSpecParseException("flow link requires a target node after -->."))

            let toPort =
                if reader.RawKind = TokenKind.Ident && not (reader.IsOnNewline()) then
                    Some(reader.ReadIdentSameLine())
                else
                    None

            Some
                { FromNode = fromNode
                  FromPort = None
                  ToNode = toNode
                  ToPort = toPort }
