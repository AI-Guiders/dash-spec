namespace DashSpec.Modeling.Parse.Palette

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Syntax

module PaletteModuleParser =

    let private unquote (raw: string) =
        let t = raw.Trim()
        if t.Length >= 2 && t.[0] = '"' && t.[t.Length - 1] = '"' then
            t.Substring(1, t.Length - 2)
        else
            t

    let rec private parseColorOperandTokens (tokens: SyntaxToken list) : string * SyntaxToken list =
        match tokens with
        | { LexKind = TokenKind.Ident; Text = fn } :: { LexKind = TokenKind.LParen } :: rest
            when String.Equals(fn, "color", StringComparison.OrdinalIgnoreCase) ->
            let inner, afterInner = parseColorOperandTokens rest
            match afterInner with
            | { LexKind = TokenKind.RParen } :: tail -> inner, tail
            | _ -> raise (DashSpecInvalidValueException("Expected ')' after color(...) operand."))
        | { LexKind = TokenKind.String; Text = s } :: tail ->
            unquote s, tail
        | { LexKind = TokenKind.HexColor; Text = h } :: tail ->
            h, tail
        | { LexKind = TokenKind.Ident; Text = id } :: tail ->
            id, tail
        | _ ->
            raise (DashSpecInvalidValueException("Expected color literal (#rrggbb), CSS name, const reference, or color(...)."))

    let private parseSingleColorOperand (tokens: SyntaxToken[]) (context: string) : string =
        let operand, rest = parseColorOperandTokens (Array.toList tokens)
        if not rest.IsEmpty then
            raise (DashSpecInvalidValueException($"Unexpected trailing tokens in {context}."))
        operand

    let private parseColorsPropertyTokens (tokens: SyntaxToken[]) (constants: IReadOnlyDictionary<string, string>) : string =
        match Array.toList tokens with
        | [ { LexKind = TokenKind.String; Text = s } ] -> unquote s
        | { LexKind = TokenKind.LBracket } :: rest ->
            let items = ResizeArray<string>()
            let mutable cur = rest
            let mutable closed = false

            while not cur.IsEmpty && not closed do
                match cur with
                | { LexKind = TokenKind.RBracket } :: tail ->
                    closed <- true
                    cur <- tail
                | _ ->
                    let operand, afterOperand = parseColorOperandTokens cur
                    items.Add operand
                    cur <-
                        match afterOperand with
                        | { LexKind = TokenKind.Comma } :: tail -> tail
                        | tail -> tail

            if not closed || not cur.IsEmpty then
                raise (DashSpecInvalidValueException("Unclosed or malformed colors list '[ ... ]'."))

            if items.Count = 0 then
                raise (DashSpecInvalidValueException("colors list requires at least one entry."))

            items
            |> Seq.map (fun operand -> PaletteColorResolver.resolveOperand operand constants "colors list")
            |> PaletteColorResolver.joinColorList
        | _ ->
            raise (DashSpecInvalidValueException("Expected colors list [ … ] or string."))

    /// Project a palette definition from any container's AST members (shared by `module palette <id>` and `palette` blocks).
    let projectFromMembers (id: string) (members: DashSpecAstNode[]) : PaletteDocument =
        let flatMembers = UnitGrammar.unwrapOptionalBlock DashSpecBlockKeyword.Palette members
        let view = UnitGrammar.view flatMembers

        if view.Body.Length <> view.Properties.Length then
            raise (DashSpecUnexpectedStatementException($"Palette '{id}' contains unexpected non-property statements."))

        let constants = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        let values = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

        for prop in view.Properties do
            match prop.KeyTokens with
            | [| { LexKind = TokenKind.Ident; Text = kw }; { LexKind = TokenKind.Ident; Text = constName } |]
                when String.Equals(kw, "const", StringComparison.OrdinalIgnoreCase) ->
                let operand = parseSingleColorOperand prop.ValueTokens $"const '{constName}'"
                let hex = PaletteColorResolver.resolveOperand operand constants $"const '{constName}'"
                constants.[constName] <- hex
            | [| { LexKind = TokenKind.Ident; Text = key } |] ->
                if String.Equals(key, "colors", StringComparison.OrdinalIgnoreCase) then
                    values.[key] <- parseColorsPropertyTokens prop.ValueTokens constants
                else
                    let operand = parseSingleColorOperand prop.ValueTokens $"palette entry '{key}'"
                    values.[key] <- PaletteColorResolver.resolveOperand operand constants $"palette entry '{key}'"
            | [| { LexKind = TokenKind.String; Text = rawKey } |] ->
                let key = unquote rawKey
                if String.IsNullOrWhiteSpace key then
                    raise (DashSpecInvalidValueException("Quoted property key cannot be empty."))
                let operand = parseSingleColorOperand prop.ValueTokens $"palette entry '{key}'"
                values.[key] <- PaletteColorResolver.resolveOperand operand constants $"palette entry '{key}'"
            | _ ->
                raise (DashSpecUnknownPropertyException(prop.Key, $"Invalid property key '{prop.Key}' in palette '{id}'."))

        if values.Count = 0 then
            raise (DashSpecMissingMemberException("colors", $"Palette '{id}' requires at least one mapping entry."))

        { Id = id; Properties = values :> IReadOnlyDictionary<_, _> }

    let parsePaletteFile (text: string) : PaletteDocument =
        if String.IsNullOrWhiteSpace text then invalidArg "text" "Palette text is required."
        let tree = DashSpecSurfaceSyntax.parse text
        let env = UnitGrammar.unwrap DashSpecModuleKind.Palette tree.Root
        projectFromMembers env.Id env.Body

    let parsePaletteFileWithId (text: string) =
        let doc = parsePaletteFile text
        doc.Id, doc
