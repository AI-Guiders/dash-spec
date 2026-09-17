namespace DashSpec.Modeling.Parse.Syntax

open System
open System.Collections.Generic
open DashSpec.Modeling.Parse.Lexing

module DashSpecSyntaxClassifier =

    let private isParseTrivia kind = kind = TokenKind.Newline || kind = TokenKind.Eof

    let private isTrivia kind =
        isParseTrivia kind || kind = TokenKind.LineComment || kind = TokenKind.BlockComment

    let private prevNonTrivia (tokens: Token[]) index =
        let mutable i = index - 1
        while i >= 0 && isTrivia tokens.[i].Kind do
            i <- i - 1
        if i >= 0 then Some i else None

    let private nextNonTrivia (tokens: Token[]) index =
        let mutable i = index + 1
        while i < tokens.Length && isTrivia tokens.[i].Kind do
            i <- i + 1
        if i < tokens.Length then Some i else None

    let private identAt (tokens: Token[]) index value =
        tokens.[index].Kind = TokenKind.Ident && DashSpecKeywords.identEquals tokens.[index].Value value

    let private matchesPhrase (tokens: Token[]) index left right =
        identAt tokens index left
        && match nextNonTrivia tokens index with
           | Some next -> identAt tokens next right
           | None -> false

    let private classifyIdent (tokens: Token[]) index =
        let value = tokens.[index].Value
        match prevNonTrivia tokens index with
        | Some prev when tokens.[prev].Kind = TokenKind.At ->
            if DashSpecKeywords.isModuleDirective value then DashSpecSyntaxKind.ModuleHeader
            else DashSpecSyntaxKind.Identifier
        | Some prev when identAt tokens prev "end" -> DashSpecSyntaxKind.Keyword
        | _ when DashSpecKeywords.identEquals value "end" -> DashSpecSyntaxKind.EndKeyword
        | _ when matchesPhrase tokens index "layout" "grid" -> DashSpecSyntaxKind.Keyword
        | _ when matchesPhrase tokens index "toolbar" "chrome" -> DashSpecSyntaxKind.Keyword
        | _ when matchesPhrase tokens index "on" "click" -> DashSpecSyntaxKind.Keyword
        | _ when matchesPhrase tokens index "filters" "chrome" -> DashSpecSyntaxKind.Keyword
        | _ when DashSpecKeywords.isControlKeyword value -> DashSpecSyntaxKind.Keyword
        | _ -> DashSpecSyntaxKind.Identifier

    let private classifyToken (tokens: Token[]) index (token: Token) =
        match token.Kind with
        | TokenKind.LineComment | TokenKind.BlockComment -> DashSpecSyntaxKind.Comment
        | TokenKind.String | TokenKind.Raw -> DashSpecSyntaxKind.String
        | TokenKind.HexColor | TokenKind.RelativeDay -> DashSpecSyntaxKind.Number
        | TokenKind.At | TokenKind.Bang -> DashSpecSyntaxKind.Keyword
        | TokenKind.Eq -> DashSpecSyntaxKind.Operator
        | TokenKind.Ident -> classifyIdent tokens index
        | TokenKind.LBrace | TokenKind.RBrace | TokenKind.Comma | TokenKind.DotDot
        | TokenKind.LBracket | TokenKind.RBracket | TokenKind.LParen | TokenKind.RParen -> DashSpecSyntaxKind.Punctuation
        | TokenKind.Newline | TokenKind.Eof -> failwith "trivia token passed to classifyToken"

    let classify (text: string) : IReadOnlyList<DashSpecSyntaxSpan> =
        let tokens = DashSpecLexer.tokenize text |> Array.ofSeq
        let spans = ResizeArray<DashSpecSyntaxSpan>()
        for i = 0 to tokens.Length - 1 do
            let token = tokens.[i]
            if not (isParseTrivia token.Kind) then
                spans.Add(
                    { Start = token.Start
                      Length = max 1 token.Length
                      Kind = classifyToken tokens i token }
                )
        spans :> IReadOnlyList<_>
