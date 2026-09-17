namespace DashSpec.Modeling.Parse.Syntax

open System
open System.Collections.Generic
open DashSpec.Modeling.Parse.Formatting
open DashSpec.Modeling.Parse.Lexing

module SyntaxTreeParser =

    type private LineSegment =
        { Span: TextSpan
          Trimmed: string
          Tokens: Token list }

    type private MutableNode =
        { Kind: SyntaxNodeKind
          Span: TextSpan
          Tokens: SyntaxToken[]
          Children: ResizeArray<MutableNode> }

    let rec private toImmutable (node: MutableNode) : SyntaxNode =
        let children = node.Children |> Seq.map toImmutable |> Array.ofSeq
        finalizeSpan
            { Kind = node.Kind
              Span = node.Span
              Tokens = node.Tokens
              Children = children }

    and private finalizeSpan (node: SyntaxNode) : SyntaxNode =
        let children = node.Children |> Array.map finalizeSpan

        let span =
            if children.Length = 0 then
                node.Span
            else
                let start =
                    children
                    |> Array.map (fun child -> child.Span.Start)
                    |> Array.append [| node.Span.Start |]
                    |> Array.min

                let stop =
                    children
                    |> Array.map (fun child -> child.Span.End)
                    |> Array.append [| node.Span.End |]
                    |> Array.max

                TextSpan.Create start (stop - start)

        { node with Span = span; Children = children }

    let private splitLines (source: string) (tokens: Token[]) =
        let lines = ResizeArray<LineSegment>()
        let current = ResizeArray<Token>()
        let mutable lineStart = 0

        let flush () =
            if current.Count = 0 then
                lines.Add({ Span = TextSpan.Create lineStart 0; Trimmed = String.Empty; Tokens = [] })
            else
                let list = current |> Seq.toList
                let start = list |> List.head |> fun t -> t.Start
                let last = List.last list
                let length = last.Start + max 1 last.Length - start
                let trimmed = source.Substring(start, length).Trim()
                lines.Add({ Span = TextSpan.Create start length; Trimmed = trimmed; Tokens = list })
            current.Clear()

        for token in tokens do
            match token.Kind with
            | TokenKind.Eof -> ()
            | TokenKind.Newline ->
                flush ()
                lineStart <- token.Start + max 1 token.Length
            | _ ->
                if current.Count = 0 then lineStart <- token.Start
                current.Add token

        flush ()
        lines :> IReadOnlyList<_>

    let private tokenIndex (allTokens: Token[]) (token: Token) =
        allTokens |> Array.findIndex (fun t -> t.Start = token.Start && t.Kind = token.Kind)

    let private lineSyntaxTokens (source: string) (allTokens: Token[]) (line: LineSegment) =
        line.Tokens
        |> List.map (fun token ->
            SyntaxTokenClassifier.toSyntaxToken source allTokens (tokenIndex allTokens token) token)
        |> Array.ofList

    let private lineNode kind (source: string) allTokens (line: LineSegment) =
        { Kind = kind
          Span = line.Span
          Tokens = lineSyntaxTokens source allTokens line
          Children = ResizeArray() }

    let private blankNode (line: LineSegment) =
        { Kind = SyntaxNodeKind.BlankLine
          Span = line.Span
          Tokens = Array.empty
          Children = ResizeArray() }

    let parse (text: string) : ParseTree =
        let tokens = DashSpecLexer.tokenize text |> Array.ofSeq
        let lines = splitLines text tokens

        let root =
            { Kind = SyntaxNodeKind.CompilationUnit
              Span = TextSpan.Create 0 (max 0 text.Length)
              Tokens = Array.empty
              Children = ResizeArray() }

        let stack = Stack<MutableNode>()
        stack.Push root

        let appendChild node =
            stack.Peek().Children.Add node

        for line in lines do
            if String.IsNullOrWhiteSpace line.Trimmed then
                appendChild (blankNode line)
            else
                match BlockFormatterRules.classifyLine line.Trimmed with
                | BlockFormatterRules.ModuleHeader _ ->
                    let node = lineNode SyntaxNodeKind.ModuleDeclaration text tokens line
                    appendChild node
                    stack.Push node
                | BlockFormatterRules.BlockOpener _ ->
                    let node = lineNode SyntaxNodeKind.Block text tokens line
                    appendChild node
                    stack.Push node
                | BlockFormatterRules.End _ ->
                    let node = lineNode SyntaxNodeKind.EndBlock text tokens line
                    appendChild node
                    if stack.Count > 1 then stack.Pop() |> ignore
                | BlockFormatterRules.BraceOpen
                | BlockFormatterRules.BraceClose
                | BlockFormatterRules.Content
                | BlockFormatterRules.Blank ->
                    appendChild (lineNode SyntaxNodeKind.Line text tokens line)

        { Text = text; Root = toImmutable root }
