namespace DashSpec.Modeling.Language.Adapters.DashSpec

open System
open AIGuiders.Platform.Modeling.Language
open DashSpec.Modeling.Parse.Lexing

/// Text/token index for DashSpec LRC navigation (ADR-0048 M8 v2).
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
        let lineIndex =
            Array.BinarySearch(lineStarts, offset)
            |> fun idx ->
                if idx >= 0 then idx
                else ~~~idx - 1

        let line = lineIndex + 1
        let column = offset - lineStarts.[lineIndex] + 1
        line, column

    let private toSpan path (lineStarts: int[]) (token: Token) =
        let line, column = lineColumn lineStarts token.Start
        let endOffset = max token.Start (token.Start + token.Length - 1)
        let endLine, endColumn = lineColumn lineStarts endOffset

        { Path = path
          Line = line
          Column = column
          EndLine = endLine
          EndColumn = endColumn + 1 }

    let private isIdentToken (token: Token) =
        token.Kind = TokenKind.Ident || token.Kind = TokenKind.Raw

    let private identEquals (left: string) (right: string) =
        String.Equals(left, right, StringComparison.OrdinalIgnoreCase)

    let private skipToIndex (tokens: Token[]) index =
        let mutable i = index

        while i < tokens.Length && (tokens.[i].Kind = TokenKind.Newline || tokens.[i].Kind = TokenKind.LineComment || tokens.[i].Kind = TokenKind.BlockComment) do
            i <- i + 1

        i

    let private tryReadIdent (tokens: Token[]) index =
        let i = skipToIndex tokens index

        if i >= tokens.Length || not (isIdentToken tokens.[i]) then
            None
        else
            Some(tokens.[i], i + 1)

    let private prevNonNewlineIndex (tokens: Token[]) index =
        let mutable i = index - 1

        while i >= 0 && (tokens.[i].Kind = TokenKind.Newline || tokens.[i].Kind = TokenKind.LineComment || tokens.[i].Kind = TokenKind.BlockComment) do
            i <- i - 1

        if i >= 0 then Some i else None

    let private isEndKeyword (tokens: Token[]) index keyword =
        match prevNonNewlineIndex tokens index with
        | None -> false
        | Some prev ->
            tokens.[prev].Kind = TokenKind.Ident
            && identEquals tokens.[prev].Value "end"
            && tokens.[index].Kind = TokenKind.Ident
            && identEquals tokens.[index].Value keyword

    let private addDefinition
        (defs: ResizeArray<SymbolDefinition>)
        path
        lineStarts
        kind
        name
        (token: Token)
        =
        defs.Add(
            { Name = name
              Kind = kind
              QualifiedName = name
              Span = toSpan path lineStarts token }
        )

    let collectDefinitions path text =
        let tokens = DashSpecLexer.tokenize text |> Array.ofSeq
        let lineStarts = buildLineStarts text
        let defs = ResizeArray<SymbolDefinition>()
        let mutable i = 0

        while i < tokens.Length do
            let token = tokens.[i]

            match token.Kind with
            | TokenKind.At ->
                match tryReadIdent tokens (i + 1) with
                | Some(directive, next) ->
                    match directive.Value.ToLowerInvariant() with
                    | "tab" ->
                        match tryReadIdent tokens next with
                        | Some(idTok, _) ->
                            addDefinition defs path lineStarts "tab" idTok.Value idTok
                        | None -> ()
                    | "dashboard" ->
                        match tryReadIdent tokens next with
                        | Some(idTok, _) ->
                            addDefinition defs path lineStarts "dashboard" idTok.Value idTok
                        | None -> ()
                    | _ -> ()
                | None -> ()
            | _ when isIdentToken token && identEquals token.Value "card" && not (isEndKeyword tokens i "card") ->
                match tryReadIdent tokens (i + 1) with
                | Some(idTok, _) when idTok.Kind = TokenKind.Ident ->
                    addDefinition defs path lineStarts "card" idTok.Value idTok
                | _ -> ()
            | _ when isIdentToken token && identEquals token.Value "filter" && not (isEndKeyword tokens i "filter") ->
                match tryReadIdent tokens (i + 1) with
                | Some(nameTok, _) when nameTok.Kind = TokenKind.Ident ->
                    addDefinition defs path lineStarts "filter" nameTok.Value nameTok
                | _ -> ()
            | _ when isIdentToken token && identEquals token.Value "diagram" ->
                match tryReadIdent tokens (i + 1) with
                | Some(idTok, _) when idTok.Kind = TokenKind.Ident ->
                    addDefinition defs path lineStarts "diagram" idTok.Value idTok
                | _ -> ()
            | _ -> ()

            i <- i + 1

        defs.ToArray()

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
                let tokens = DashSpecLexer.tokenize text

                tokens
                |> Seq.tryFind (fun token ->
                    isIdentToken token
                    && token.Start <= targetOffset
                    && token.Start + token.Length > targetOffset
                    && token.Start >= lineStart
                    && token.Start <= lineEnd)
                |> Option.map (fun token -> token, toSpan "" lineStarts token)

    let resolveSymbolAt path text line column =
        match findIdentTokenAt text line column with
        | None -> None
        | Some(token, span) ->
            let lineStarts = buildLineStarts text
            let span = { span with Path = path }
            let defs = collectDefinitions path text

            match Array.tryFind (fun def -> identEquals def.Name token.Value) defs with
            | Some def -> Some def
            | None ->
                Some
                    { Name = token.Value
                      Kind = "reference"
                      QualifiedName = token.Value
                      Span = span }

    let findIdentReferences path text symbolName =
        let lineStarts = buildLineStarts text
        let tokens = DashSpecLexer.tokenize text

        tokens
        |> Seq.filter (fun token -> isIdentToken token && identEquals token.Value symbolName)
        |> Seq.map (fun token -> toSpan path lineStarts token)
        |> Array.ofSeq

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
            |> Array.map (fun span ->
                { Span = span
                  Target = target
                  Kind = symbol.Kind })

    let goToDefinition path text line column =
        match resolveSymbolAt path text line column with
        | None -> None
        | Some symbol ->
            let defs = collectDefinitions path text

            match Array.tryFind (fun def -> identEquals def.Name symbol.Name) defs with
            | Some def -> Some def.Span
            | None -> Some symbol.Span
