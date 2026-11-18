namespace DashSpec.Modeling.Parse.Formatting

open System
open DashSpec.Modeling.Parse.Lexing

/// Classifies one physical line from already-lexed tokens (no per-line re-lex in syntax tree build).
module BlockSurfaceLineClassifier =

    type LineKind =
        | Blank
        | End of kind: string * id: string option
        | ModuleHeader of kind: string * id: string
        | BlockOpener of endKind: string * endId: string option
        | BraceOpen
        | BraceClose
        | Content

    let private isSignificant kind =
        kind <> TokenKind.Newline
        && kind <> TokenKind.Eof
        && kind <> TokenKind.LineComment
        && kind <> TokenKind.BlockComment

    let private significantTokens (tokens: Token list) =
        tokens |> List.filter (fun t -> isSignificant t.Kind)

    let private tryReadOptionalId (tokens: Token list) =
        match tokens with
        | { Kind = TokenKind.Ident; Value = id } :: _ -> Some id
        | _ -> None

    let private hasEquals (tokens: Token list) =
        tokens |> List.exists (fun t -> t.Kind = TokenKind.Eq)

    let private (|Kw|_|) (token: Token) : DashSpec.Modeling.Parse.Syntax.DashSpecKeyword option =
        if token.Kind = TokenKind.Ident then
            DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.tryParse token.Value
        else
            None

    /// Classify a line from tokens between newlines using typed `DashSpecKeyword` active patterns.
    let classifyLineTokens (tokens: Token list) : LineKind =
        let tokens = significantTokens tokens

        if tokens.IsEmpty then
            Blank
        else
            match tokens with
            | [ { Kind = TokenKind.LBrace } ] -> BraceOpen
            | { Kind = TokenKind.RBrace } :: _ -> BraceClose
            | Kw DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.End :: { Kind = TokenKind.Ident; Value = kind } :: rest ->
                End(kind, tryReadOptionalId rest)
            | Kw DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.Module :: { Kind = TokenKind.Ident; Value = kind } :: { Kind = TokenKind.Ident; Value = id } :: _ ->
                ModuleHeader(kind, id)
            | Kw (DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.Block DashSpec.Modeling.Parse.Syntax.DashSpecBlockKeyword.Layout)
              :: Kw (DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.Block DashSpec.Modeling.Parse.Syntax.DashSpecBlockKeyword.Grid) :: _ ->
                BlockOpener("grid", None)
            | Kw (DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.Block DashSpec.Modeling.Parse.Syntax.DashSpecBlockKeyword.Toolbar)
              :: Kw (DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.Block DashSpec.Modeling.Parse.Syntax.DashSpecBlockKeyword.Chrome) :: _ ->
                BlockOpener("chrome", None)
            | Kw DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.On
              :: Kw (DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.Block DashSpec.Modeling.Parse.Syntax.DashSpecBlockKeyword.Click) :: _ ->
                BlockOpener("click", None)
            | Kw (DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.Block DashSpec.Modeling.Parse.Syntax.DashSpecBlockKeyword.Filters)
              :: Kw (DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.Block DashSpec.Modeling.Parse.Syntax.DashSpecBlockKeyword.Chrome) :: _ ->
                BlockOpener("chrome", None)
            | Kw DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.From :: rest when
                rest |> List.exists (function Kw DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.Import -> true | _ -> false) ->
                let importIdx =
                    rest
                    |> List.findIndex (function Kw DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.Import -> true | _ -> false)
                let nsTokens = rest |> List.take importIdx
                let afterImport = rest |> List.skip (importIdx + 1)
                let nsName = nsTokens |> List.map (fun t -> t.Value) |> String.concat ""
                match afterImport with
                | []
                | [ { Kind = TokenKind.LBrace } ] ->
                    BlockOpener("import", Some nsName)
                | _ ->
                    Content
            | { Kind = TokenKind.Ident; Value = kw } :: { Kind = TokenKind.Ident; Value = id } :: _
                when DashSpec.Modeling.Parse.Syntax.DashSpecBlockKeyword.isNamedBlockKeyword kw && id <> "as" ->
                BlockOpener(kw, Some id)
            | Kw (DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.Block DashSpec.Modeling.Parse.Syntax.DashSpecBlockKeyword.Transform)
              :: Kw DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.Use :: { Kind = TokenKind.Ident; Value = pluginId } :: _ ->
                BlockOpener("transform", Some pluginId)
            | Kw (DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.Block DashSpec.Modeling.Parse.Syntax.DashSpecBlockKeyword.Transform)
              :: Kw (DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.Block DashSpec.Modeling.Parse.Syntax.DashSpecBlockKeyword.Series) :: _ ->
                BlockOpener("series", None)
            | Kw (DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.Block DashSpec.Modeling.Parse.Syntax.DashSpecBlockKeyword.Transform) :: _ ->
                Content
            | tokens when hasEquals tokens -> Content
            | Kw (DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.Block DashSpec.Modeling.Parse.Syntax.DashSpecBlockKeyword.Filter) :: _ -> Content
            | Kw DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.Use :: _ -> Content
            | { Kind = TokenKind.Bang } :: _ -> Content
            | (Kw (DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.Block blk) as tok)
              :: { Kind = TokenKind.Colon }
              :: { Kind = TokenKind.Ident; Value = w } :: rest
              when (blk = DashSpec.Modeling.Parse.Syntax.DashSpecBlockKeyword.VStack
                    || blk = DashSpec.Modeling.Parse.Syntax.DashSpecBlockKeyword.HStack)
                   && (Int32.TryParse w |> fst)
                   && (match rest with [] | [ { Kind = TokenKind.LBrace } ] -> true | _ -> false) ->
                BlockOpener(tok.Value, None)
            | _ :: { Kind = TokenKind.Colon } :: _ ->
                Content
            | [ Kw (DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.Block DashSpec.Modeling.Parse.Syntax.DashSpecBlockKeyword.Diagram) ] ->
                Content
            | (Kw (DashSpec.Modeling.Parse.Syntax.DashSpecKeyword.Block _) as tok) :: _ ->
                BlockOpener(tok.Value, None)
            | _ -> Content

    let classifyTrimmedLine (trimmed: string) : LineKind =
        if String.IsNullOrWhiteSpace trimmed then
            Blank
        elif trimmed = "{" then
            BraceOpen
        elif trimmed = "}" || trimmed.StartsWith("}", StringComparison.Ordinal) then
            BraceClose
        else
            let tokens =
                DashSpecLexer.tokenize trimmed
                |> Seq.filter (fun t -> isSignificant t.Kind)
                |> Seq.toList

            classifyLineTokens tokens
