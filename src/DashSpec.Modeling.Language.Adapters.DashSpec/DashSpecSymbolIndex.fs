namespace DashSpec.Modeling.Language.Adapters.DashSpec

open System
open AIGuiders.Platform.Modeling.Language
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Syntax

/// AST & typed-token symbol index for DashSpec LRC navigation (ADR-0048 M8 / ADR-0130).
module DashSpecSymbolIndex =

    type SymbolDefinition =
        { Name: string
          Kind: string
          QualifiedName: string
          Span: SourceSpan }

    let private buildLineStarts (text: string) =
        let starts = ResizeArray<int>()
        starts.Add(0)

        for i = 0 to text.Length - 1 do
            if text.[i] = '\n' then
                starts.Add(i + 1)

        starts.ToArray()

    let private lineColumn (lineStarts: int[]) (offset: int) =
        let clamped = max 0 (min offset (if lineStarts.Length > 0 then Int32.MaxValue else 0))
        let lineIndex =
            Array.BinarySearch(lineStarts, clamped)
            |> fun idx ->
                if idx >= 0 then idx
                else max 0 (~~~idx - 1)

        let line = lineIndex + 1
        let column = max 1 (clamped - lineStarts.[lineIndex] + 1)
        line, column

    let private toSpan path (lineStarts: int[]) (span: TextSpan) =
        let line, column = lineColumn lineStarts span.Start
        let endOffset = max span.Start (span.End - 1)
        let endLine, endColumn = lineColumn lineStarts endOffset

        { Path = path
          Line = line
          Column = column
          EndLine = endLine
          EndColumn = endColumn + 1 }

    let private isIdentLexKind (kind: TokenKind) =
        kind = TokenKind.Ident || kind = TokenKind.Raw

    let private identEquals (left: string) (right: string) =
        String.Equals(left, right, StringComparison.OrdinalIgnoreCase)

    let private tryDefinitionFromAstNode (path: string) (lineStarts: int[]) (node: DashSpecAstNode) : SymbolDefinition option =
        match node with
        | DashSpecAstNode.ModuleDeclaration m ->
            let kind, id =
                match m.Directive with
                | DashSpecModuleDirective.Dashboard id -> "dashboard", id
                | DashSpecModuleDirective.Tab id -> "tab", id
                | DashSpecModuleDirective.Module(k, id) -> DashSpecModuleKind.toKeyword k, id
            let span =
                m.Tokens
                |> Array.tryFind (fun t -> isIdentLexKind t.LexKind && identEquals t.Text id)
                |> Option.map (fun t -> t.Span)
                |> Option.defaultValue m.HeaderSpan
            Some
                { Name = id
                  Kind = kind
                  QualifiedName = id
                  Span = toSpan path lineStarts span }
        | DashSpecAstNode.BlockDeclaration b ->
            match b.Opener with
            | DashSpecBlockOpener.Named(kw, id) ->
                let kind = DashSpecBlockKeyword.toEndName kw
                let span =
                    b.Tokens
                    |> Array.tryFind (fun t -> isIdentLexKind t.LexKind && identEquals t.Text id)
                    |> Option.map (fun t -> t.Span)
                    |> Option.defaultValue b.HeaderSpan
                Some
                    { Name = id
                      Kind = kind
                      QualifiedName = id
                      Span = toSpan path lineStarts span }
            | DashSpecBlockOpener.Anonymous _ -> None
        | DashSpecAstNode.CardReference c ->
            Some
                { Name = c.CardId
                  Kind = "card"
                  QualifiedName = c.CardId
                  Span = toSpan path lineStarts c.Span }
        | DashSpecAstNode.TypeFieldDeclaration f ->
            Some
                { Name = f.FieldName
                  Kind = "field"
                  QualifiedName = f.FieldName
                  Span = toSpan path lineStarts f.Span }
        | DashSpecAstNode.PortDeclaration p ->
            Some
                { Name = p.PortName
                  Kind = "port"
                  QualifiedName = p.PortName
                  Span = toSpan path lineStarts p.Span }
        | DashSpecAstNode.TableRowDeclaration { Kind = TableRowKind.Filter f } when not (String.IsNullOrWhiteSpace f.Id) ->
            Some
                { Name = f.Id
                  Kind = "filter"
                  QualifiedName = f.Id
                  Span = toSpan path lineStarts (DashSpecAst.span node) }
        | DashSpecAstNode.Line l ->
            match l.Tokens with
            | [| { Keyword = Some(DashSpecKeyword.Block DashSpecBlockKeyword.Filter) }; idTok |]
                when isIdentLexKind idTok.LexKind ->
                Some
                    { Name = idTok.Text
                      Kind = "filter"
                      QualifiedName = idTok.Text
                      Span = toSpan path lineStarts idTok.Span }
            | _ -> None
        | _ -> None

    let collectDefinitions path text =
        let tree = SyntaxTree.parse text
        let lineStarts = buildLineStarts text
        DashSpecAst.descendants tree.Root
        |> Seq.choose (tryDefinitionFromAstNode path lineStarts)
        |> Array.ofSeq

    let findIdentTokenAt text line column =
        if line < 1 || column < 1 then
            None
        else
            let lineStarts = buildLineStarts text

            if line > lineStarts.Length then
                None
            else
                let lineStart = lineStarts.[line - 1]
                let lineEnd =
                    if line >= lineStarts.Length then text.Length
                    else lineStarts.[line] - 1

                let targetOffset = lineStart + column - 1
                let tree = SyntaxTree.parse text

                tree.Tokens
                |> Array.tryFind (fun token ->
                    isIdentLexKind token.LexKind
                    && token.Span.Start <= targetOffset
                    && token.Span.End > targetOffset
                    && token.Span.Start >= lineStart
                    && token.Span.Start <= lineEnd)
                |> Option.map (fun token -> token, toSpan "" lineStarts token.Span)

    let resolveSymbolAt path text line column =
        match findIdentTokenAt text line column with
        | None -> None
        | Some(token, span) ->
            let span = { span with Path = path }
            let defs = collectDefinitions path text

            match Array.tryFind (fun def -> identEquals def.Name token.Text) defs with
            | Some def -> Some def
            | None ->
                Some
                    { Name = token.Text
                      Kind = "reference"
                      QualifiedName = token.Text
                      Span = span }

    let findIdentReferences path text symbolName =
        let lineStarts = buildLineStarts text
        let tree = SyntaxTree.parse text

        tree.Tokens
        |> Array.filter (fun token -> isIdentLexKind token.LexKind && identEquals token.Text symbolName)
        |> Array.map (fun token ->
            let role = DashSpecAst.outlineLabelAt token.Span.Start tree.Root
            toSpan path lineStarts token.Span, role)

    let findReferences path text line column =
        match resolveSymbolAt path text line column with
        | None -> [||]
        | Some symbol ->
            let defs = collectDefinitions path text

            let target =
                match Array.tryFind (fun def -> identEquals def.Name symbol.Name) defs with
                | Some def -> def.Span
                | None -> symbol.Span

            findIdentReferences path text symbol.Name
            |> Array.map (fun (span, role) ->
                { Span = span
                  Target = target
                  Kind = role })

    let goToDefinition path text line column =
        match resolveSymbolAt path text line column with
        | None -> None
        | Some symbol ->
            let defs = collectDefinitions path text

            match Array.tryFind (fun def -> identEquals def.Name symbol.Name) defs with
            | Some def -> Some def.Span
            | None -> Some symbol.Span

    /// Replace every typed identifier token matching <paramref name="oldName"/> with <paramref name="newName"/>
    /// using the parsed <see cref="SyntaxTree.Tokens"/> spans (no regex).
    let renameIdentTokens (text: string) (oldName: string) (newName: string) : string =
        if String.IsNullOrEmpty text || String.IsNullOrWhiteSpace oldName || String.IsNullOrWhiteSpace newName then
            text
        else
            let tree = SyntaxTree.parse text
            let matching =
                tree.Tokens
                |> Array.filter (fun t -> isIdentLexKind t.LexKind && identEquals t.Text oldName)
                |> Array.sortByDescending (fun t -> t.Span.Start)

            if matching.Length = 0 then
                text
            else
                let sb = System.Text.StringBuilder(text)
                for tok in matching do
                    if tok.Span.Start >= 0 && tok.Span.End <= sb.Length then
                        sb.Remove(tok.Span.Start, tok.Span.Length) |> ignore
                        sb.Insert(tok.Span.Start, newName) |> ignore
                sb.ToString()
