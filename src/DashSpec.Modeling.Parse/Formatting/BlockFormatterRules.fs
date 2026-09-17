namespace DashSpec.Modeling.Parse.Formatting

open System
open DashSpec.Modeling.Parse.Lexing

module BlockFormatterRules =

    type LineKind =
        | Blank
        | End of kind: string * id: string option
        | ModuleHeader of kind: string * id: string
        | BlockOpener of endKind: string * endId: string option
        | BraceOpen
        | BraceClose
        | Content

    let private lineTokens (trimmed: string) =
        if String.IsNullOrWhiteSpace trimmed then []
        else
            DashSpecLexer.tokenize trimmed
            |> Seq.filter (fun t ->
                t.Kind <> TokenKind.Newline
                && t.Kind <> TokenKind.Eof
                && t.Kind <> TokenKind.LineComment
                && t.Kind <> TokenKind.BlockComment)
            |> Seq.toList

    let private tryReadOptionalId (tokens: Token list) =
        match tokens with
        | { Kind = TokenKind.Ident; Value = id } :: _ -> Some id
        | _ -> None

    let private hasEquals (tokens: Token list) =
        tokens |> List.exists (fun t -> t.Kind = TokenKind.Eq)

    let classifyLine (trimmed: string) =
        if String.IsNullOrWhiteSpace trimmed then Blank
        elif trimmed = "{" then BraceOpen
        elif trimmed = "}" || trimmed.StartsWith("}", StringComparison.Ordinal) then BraceClose
        else
            match lineTokens trimmed with
            | [] -> Blank
            | { Kind = TokenKind.Ident; Value = "end" } :: { Kind = TokenKind.Ident; Value = kind } :: rest ->
                End(kind, tryReadOptionalId rest)
            | { Kind = TokenKind.At } :: { Kind = TokenKind.Ident; Value = kind } :: { Kind = TokenKind.Ident; Value = id } :: _ ->
                ModuleHeader(kind, id)
            | { Kind = TokenKind.Ident; Value = "layout" } :: { Kind = TokenKind.Ident; Value = "grid" } :: _ ->
                BlockOpener("grid", None)
            | { Kind = TokenKind.Ident; Value = "toolbar" } :: { Kind = TokenKind.Ident; Value = "chrome" } :: _ ->
                BlockOpener("chrome", None)
            | { Kind = TokenKind.Ident; Value = "on" } :: { Kind = TokenKind.Ident; Value = "click" } :: _ ->
                BlockOpener("click", None)
            | { Kind = TokenKind.Ident; Value = "filters" } :: { Kind = TokenKind.Ident; Value = "chrome" } :: _ ->
                BlockOpener("chrome", None)
            | { Kind = TokenKind.Ident; Value = "card" } :: { Kind = TokenKind.Ident; Value = id } :: _ ->
                BlockOpener("card", Some id)
            | { Kind = TokenKind.Ident; Value = "tab" } :: { Kind = TokenKind.Ident; Value = id } :: _ ->
                BlockOpener("tab", Some id)
            | { Kind = TokenKind.Ident; Value = "page" } :: { Kind = TokenKind.Ident; Value = id } :: _ ->
                BlockOpener("page", Some id)
            | { Kind = TokenKind.Ident; Value = "phase" } :: { Kind = TokenKind.Ident; Value = id } :: _ ->
                BlockOpener("phase", Some id)
            | { Kind = TokenKind.Ident; Value = "group" } :: { Kind = TokenKind.Ident; Value = id } :: _ ->
                BlockOpener("group", Some id)
            | tokens when hasEquals tokens -> Content
            | { Kind = TokenKind.Ident; Value = kw } :: _ when DashSpecKeywords.isBlockKeyword kw ->
                BlockOpener(kw, None)
            | { Kind = TokenKind.Ident; Value = "filter" } :: _ -> Content
            | { Kind = TokenKind.Bang } :: _ -> Content
            | { Kind = TokenKind.Ident; Value = "use" } :: _ -> Content
            | _ -> Content
