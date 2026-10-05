namespace DashSpec.Modeling.Parse.DataFlow

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

/// Flow links: <c>producer [outPort] -&gt; [inPort] consumer</c> (brackets = port names; left = output, right = input).
module FlowLinkParser =

    let private readDottedSameLine (reader: TokenReader) =
        let root = reader.ReadIdentSameLine()

        let rec extend (left: string) =
            if reader.IsOnNewline() then
                left
            elif not (reader.IsAt TokenKind.Dot) then
                left
            else
                reader.Advance()

                if reader.IsOnNewline() then
                    raise (DashSpecParseException("flow link port requires an identifier after '.'."))

                extend $"{left}.{reader.ReadIdentSameLine()}"

        extend root

    /// Optional <c>[port]</c> on the same line (output side before arrow, input side after arrow).
    let tryReadBracketPortSameLine (reader: TokenReader) =
        if reader.IsOnNewline() then
            None
        elif reader.RawKind <> TokenKind.LBracket then
            None
        else
            reader.Advance()
            let portName = readDottedSameLine reader

            if String.IsNullOrWhiteSpace portName then
                raise (DashSpecParseException("flow link port name is required inside []."))

            if reader.RawKind <> TokenKind.RBracket then
                raise (DashSpecParseException("flow link port requires closing ']'."))

            reader.Advance()
            Some portName

    let tryParseLink (reader: TokenReader) (fromNode: string) (fromPort: string option) =
        if not (reader.IsAt TokenKind.FlowArrow) then
            None
        else
            reader.Advance()
            let toPortFromBracket = tryReadBracketPortSameLine reader
            let toNode = reader.ReadIdent()

            if String.IsNullOrWhiteSpace toNode then
                raise (DashSpecParseException("flow link requires a target node after ->."))

            Some
                { FromNode = fromNode
                  FromPort = fromPort
                  ToNode = toNode
                  ToPort = toPortFromBracket }
