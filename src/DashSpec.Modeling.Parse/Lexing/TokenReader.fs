namespace DashSpec.Modeling.Parse.Lexing

open System
open System.Collections.Generic
open DashSpec.Modeling.Core

/// Token cursor over lexed dashspec input.
type TokenReader(tokens: IReadOnlyList<Token>) =
    let mutable index = 0
    let blockCloseStyles = Stack<BlockCloseStyle>()
    let mutable consumedRuntimePath: string option = None
    let mutable consumedDiagramLibraryPath: string option = None
    let mutable consumedPalettePath: string option = None

    member _.ConsumedRuntimePath = consumedRuntimePath
    member _.ConsumedDiagramLibraryPath = consumedDiagramLibraryPath
    member _.ConsumedPalettePath = consumedPalettePath

    member _.Current = tokens.[index]

    member _.RawKind = tokens.[index].Kind

    member _.IsOnNewline() = tokens.[index].Kind = TokenKind.Newline

    member private _.IsTrivia kind =
        kind = TokenKind.Newline || kind = TokenKind.LineComment || kind = TokenKind.BlockComment

    member this.SkipNewlines() =
        while this.IsTrivia tokens.[index].Kind do
            index <- index + 1

    member this.Advance() = index <- index + 1

    member this.IsEof =
        this.SkipNewlines()
        tokens.[index].Kind = TokenKind.Eof

    member this.IsAt kind =
        this.SkipNewlines()
        tokens.[index].Kind = kind

    member this.Expect kind =
        this.SkipNewlines()
        if tokens.[index].Kind <> kind then
            raise (this.Unexpected(string kind))
        index <- index + 1

    member this.TryKeyword keyword =
        this.SkipNewlines()
        if tokens.[index].Kind <> TokenKind.Ident then false
        elif not (String.Equals(tokens.[index].Value, keyword, StringComparison.OrdinalIgnoreCase)) then false
        else
            index <- index + 1
            true

    member this.TryKeywordSameLine keyword =
        match tokens.[index].Kind with
        | TokenKind.Newline | TokenKind.Eof -> false
        | TokenKind.Ident when String.Equals(tokens.[index].Value, keyword, StringComparison.OrdinalIgnoreCase) ->
            index <- index + 1
            true
        | _ -> false

    member this.ExpectKeyword keyword =
        if not (this.TryKeyword keyword) then
            raise (this.Unexpected keyword)

    member this.ReadIdent() =
        this.SkipNewlines()
        if tokens.[index].Kind <> TokenKind.Ident then
            raise (this.Unexpected "identifier")
        let value = tokens.[index].Value
        index <- index + 1
        value

    member this.ReadIdentSameLine() =
        if tokens.[index].Kind <> TokenKind.Ident then
            raise (this.Unexpected "identifier on the same line")
        let value = tokens.[index].Value
        index <- index + 1
        value

    member _.PushBlockClose style = blockCloseStyles.Push style

    member _.PopBlockClose() =
        if blockCloseStyles.Count > 0 then blockCloseStyles.Pop() |> ignore

    member _.PeekBlockClose() =
        if blockCloseStyles.Count > 0 then blockCloseStyles.Peek() else BlockCloseStyle.EndKeyword

    member _.SavePosition() = index

    member _.RestorePosition position = index <- position

    member this.Unexpected(?expected: string) =
        let token = tokens.[index]
        let message =
            match expected with
            | Some e -> $"Expected {e}, got '{token.Value}' ({token.Kind}) at position {token.Start}."
            | None -> $"Unexpected token '{token.Value}' ({token.Kind}) at position {token.Start}."
        DashSpec.Modeling.Core.DashSpecParseException(message, token.Start)

    member this.ReadString() =
        this.SkipNewlines()
        if tokens.[index].Kind <> TokenKind.String then
            raise (this.Unexpected "string")
        let value = tokens.[index].Value
        index <- index + 1
        value

    member this.CurrentKind =
        this.SkipNewlines()
        tokens.[index].Kind

    member this.ReadPropertyKey(?allowQuoted: bool) =
        let allowQuoted = defaultArg allowQuoted false
        this.SkipNewlines()
        if allowQuoted && tokens.[index].Kind = TokenKind.String then this.ReadString()
        else this.ReadIdent()

    member this.ReadScalarValue() =
        this.SkipNewlines()
        match tokens.[index].Kind with
        | TokenKind.String -> this.ReadString()
        | TokenKind.Ident -> this.ReadIdent()
        | TokenKind.RelativeDay ->
            let value = tokens.[index].Value
            index <- index + 1
            value
        | _ -> raise (this.Unexpected "scalar value")

    member this.ReadHexColor() =
        this.SkipNewlines()
        if tokens.[index].Kind <> TokenKind.HexColor then
            raise (this.Unexpected "#rrggbb or #rgb hex color")
        let value = tokens.[index].Value
        index <- index + 1
        value

    member this.ReadQualifiedName() =
        this.SkipNewlines()
        match tokens.[index].Kind with
        | TokenKind.Ident | TokenKind.Raw ->
            let value = tokens.[index].Value
            index <- index + 1
            value
        | _ -> raise (this.Unexpected "qualified name")

    member this.TryPeekIdent() =
        this.SkipNewlines()
        if tokens.[index].Kind <> TokenKind.Ident then None
        else Some tokens.[index].Value

    member this.ReadRawBlock() =
        this.SkipNewlines()
        if tokens.[index].Kind <> TokenKind.Raw then
            raise (this.Unexpected "[[ … ]] raw block")
        let value = tokens.[index].Value
        index <- index + 1
        value

    member private this.ReadRelativeDay() =
        this.SkipNewlines()
        if tokens.[index].Kind <> TokenKind.RelativeDay then
            raise (this.Unexpected "relative day")
        let value = tokens.[index].Value
        index <- index + 1
        value

    member private this.ReadDateBoundToken() =
        this.SkipNewlines()
        match tokens.[index].Kind with
        | TokenKind.RelativeDay -> this.ReadRelativeDay()
        | TokenKind.Ident -> this.ReadIdent()
        | _ -> raise (this.Unexpected "date bound (today, -Nd, yyyy-MM-dd)")

    member this.ReadDateDefaultValue() =
        let from = this.ReadDateBoundToken()
        if this.RawKind <> TokenKind.DotDot then from
        else
            index <- index + 1
            let toValue = this.ReadDateBoundToken()
            $"{from}..{toValue}"

    member private this.ReadListItem() =
        this.SkipNewlines()
        match tokens.[index].Kind with
        | TokenKind.Ident -> this.ReadIdent()
        | TokenKind.String -> this.ReadString()
        | _ -> raise (this.Unexpected "list item")

    member this.ReadCommaSeparatedValues() =
        let parts = ResizeArray<string>()
        parts.Add(this.ReadListItem())
        while this.CurrentKind = TokenKind.Comma do
            index <- index + 1
            parts.Add(this.ReadListItem())
        String.Join(", ", parts)

    member this.ReadCommaListInline() =
        let names = ResizeArray<string>()
        names.Add(this.ReadIdent())
        while this.CurrentKind = TokenKind.Comma do
            index <- index + 1
            names.Add(this.ReadIdent())
        names :> IReadOnlyList<_>

    member this.ReadColumnBinding() =
        let column = this.ReadQualifiedName()
        if this.TryKeyword "as" then
            { Column = column; Alias = Some(this.ReadString()) }
        elif tokens.[index].Kind = TokenKind.Ident && not (this.IsOnNewline()) then
            let saved = this.SavePosition()
            let alias = this.ReadIdent()
            if this.RawKind = TokenKind.Eq then
                this.RestorePosition saved
                { Column = column; Alias = None }
            elif String.Equals(alias, "end", StringComparison.OrdinalIgnoreCase) then
                this.RestorePosition saved
                { Column = column; Alias = None }
            else
                { Column = column; Alias = Some alias }
        else
            { Column = column; Alias = None }

    member this.ReadRestOfLine() =
        let parts = ResizeArray<string>()
        while tokens.[index].Kind = TokenKind.Ident || tokens.[index].Kind = TokenKind.Raw do
            parts.Add(tokens.[index].Value)
            index <- index + 1
        String.Join(' ', parts)

    member this.TryModuleInclude() =
        this.SkipNewlines()
        if this.TryKeyword "import" then
            Some(this.ReadString())
        elif not (this.IsAt TokenKind.Bang) then
            None
        else
            this.Advance()
            if not (this.TryKeyword "include") then
                raise (DashSpecParseException("Expected 'include' after '!'."))
            Some(this.ReadString())

    member this.SkipFileDirectives() =
        this.SkipNewlines()
        let mutable continueDirectives = true

        while continueDirectives && this.IsAt TokenKind.At do
            this.Advance()

            if this.TryKeyword "runtime" || this.TryKeyword "config" then
                if consumedRuntimePath.IsSome then
                    raise (DashSpecParseException("Only one @runtime directive is allowed per .dashspec file."))
                if this.IsAt TokenKind.String then
                    consumedRuntimePath <- Some(this.ReadString())
                this.SkipNewlines()
            elif this.TryKeyword "sqldialect" then
                this.ReadIdent() |> ignore
                this.SkipNewlines()
            elif this.TryKeyword "diagramlibrary" then
                if consumedDiagramLibraryPath.IsSome then
                    raise (DashSpecParseException("Only one @diagramlibrary directive is allowed per .dashspec file."))
                if this.IsAt TokenKind.String then
                    consumedDiagramLibraryPath <- Some(this.ReadString())
                this.SkipNewlines()
            elif this.TryKeyword "palette" then
                this.SkipNewlines()

                if this.RawKind = TokenKind.String then
                    if consumedPalettePath.IsSome then
                        raise (DashSpecParseException("Only one @palette file directive is allowed per .dashspec file."))
                    consumedPalettePath <- Some(this.ReadString())
                    this.SkipNewlines()
                else
                    index <- index - 2
                    continueDirectives <- false
            else
                index <- index - 1
                continueDirectives <- false