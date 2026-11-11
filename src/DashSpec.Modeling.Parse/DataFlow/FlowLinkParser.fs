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
                    raise (DashSpecParseException("flow link endpoint requires an identifier after '.'."))

                extend $"{left}.{reader.ReadIdentSameLine()}"

        extend root

    /// Reads a link endpoint (e.g. <c>host.chrome.card.events_detail</c>) on the current line.
    let readEndpointSameLine (reader: TokenReader) =
        if reader.IsOnNewline() then
            raise (DashSpecParseException("flow link endpoint is required."))

        readDottedSameLine reader

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
            let toNode = readEndpointSameLine reader

            if String.IsNullOrWhiteSpace toNode then
                raise (DashSpecParseException("flow link requires a target node after ->."))

            Some
                { FromNode = fromNode
                  FromPort = fromPort
                  ToNode = toNode
                  ToPort = toPortFromBracket }

    /// <summary>ADR-0130 slice 2: project flow links from typed AST members. None when
    /// any member is neither a link, trivia, nor a blank — the caller falls back.</summary>
    let tryProjectLinksFromAst (members: DashSpec.Modeling.Parse.Syntax.DashSpecAstNode[]) : FlowLinkDef list option =
        let mutable ok = true
        let links = ResizeArray<FlowLinkDef>()

        for memberNode in members do
            match memberNode with
            | DashSpec.Modeling.Parse.Syntax.DashSpecAstNode.FlowLinkDeclaration link ->
                links.Add
                    { FromNode = link.FromNode
                      FromPort = link.FromPort
                      ToNode = link.ToNode
                      ToPort = link.ToPort }
            | DashSpec.Modeling.Parse.Syntax.DashSpecAstNode.BlankLine _
            | DashSpec.Modeling.Parse.Syntax.DashSpecAstNode.EndBlock _ -> ()
            | memberNode when DashSpec.Modeling.Parse.Syntax.DashSpecAst.isTrivia memberNode -> ()
            | _ -> ok <- false

        if ok then Some(List.ofSeq links) else None
