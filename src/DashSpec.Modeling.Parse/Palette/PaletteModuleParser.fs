namespace DashSpec.Modeling.Parse.Palette

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module PaletteModuleParser =

    let rec private readColorOperand (reader: TokenReader) =
        reader.SkipNewlines()
        if reader.IsAt TokenKind.Ident then
            let saved = reader.SavePosition()
            let name = reader.ReadIdent()
            if name.Equals("color", StringComparison.OrdinalIgnoreCase) && reader.IsAt TokenKind.LParen then
                reader.Expect TokenKind.LParen
                let inner = readColorOperand reader
                reader.Expect TokenKind.RParen
                inner
            else
                reader.RestorePosition saved
                reader.ReadIdent()
        elif reader.IsAt TokenKind.String then reader.ReadString()
        elif reader.IsAt TokenKind.HexColor then reader.ReadHexColor()
        elif reader.IsAt TokenKind.Ident then reader.ReadIdent()
        else raise (reader.Unexpected "color literal (#rrggbb), CSS name, const reference, or color(...)")

    let private readColorList (reader: TokenReader) =
        reader.Expect TokenKind.LBracket
        reader.SkipNewlines()
        let items = ResizeArray<string>()

        while not (reader.IsAt TokenKind.RBracket) && not reader.IsEof do
            reader.SkipNewlines()
            if reader.IsAt TokenKind.RBracket then ()
            else
                items.Add(readColorOperand reader)
                reader.SkipNewlines()
                if reader.CurrentKind = TokenKind.Comma then reader.Advance()

        reader.SkipNewlines()
        reader.Expect TokenKind.RBracket
        if items.Count = 0 then raise (DashSpecParseException("colors list requires at least one entry."))
        items :> IReadOnlyList<_>

    let private readColorsProperty (reader: TokenReader) (constants: IReadOnlyDictionary<string, string>) =
        if reader.IsAt TokenKind.LBracket then
            readColorList reader
            |> Seq.map (fun operand -> PaletteColorResolver.resolveOperand operand constants "colors list")
            |> PaletteColorResolver.joinColorList
        elif reader.CurrentKind = TokenKind.String then reader.ReadString()
        else raise (reader.Unexpected "colors list [ … ] or string")

    let private parseConstants (reader: TokenReader) =
        let constants = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        while reader.TryKeyword "const" do
            let name = reader.ReadIdent()
            if String.IsNullOrWhiteSpace name then raise (DashSpecParseException("const requires a name."))
            reader.Expect TokenKind.Eq
            let operand = readColorOperand reader
            let hex = PaletteColorResolver.resolveOperand operand constants $"const '{name}'"
            constants.[name] <- hex
            reader.SkipNewlines()
        constants :> IReadOnlyDictionary<string, string>

    let private parsePaletteMappings (reader: TokenReader) (constants: IReadOnlyDictionary<string, string>) wrapped endKind =
        let values = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

        let shouldContinue () =
            if wrapped then not (reader.IsAt TokenKind.RBrace) && not reader.IsEof
            elif not (String.IsNullOrWhiteSpace endKind) then not (BlockSyntax.isBlockEnd reader endKind None) && not reader.IsEof
            else not reader.IsEof

        while shouldContinue () do
            reader.SkipNewlines()
            if wrapped && reader.IsAt TokenKind.RBrace then ()
            elif not (String.IsNullOrWhiteSpace endKind) && BlockSyntax.isBlockEnd reader endKind None then ()
            elif not wrapped && reader.IsEof then ()
            else
                let inlineContinue () =
                    if wrapped then not (reader.IsAt TokenKind.RBrace) && not reader.IsEof
                    elif not (String.IsNullOrWhiteSpace endKind) then
                        not (reader.IsAt TokenKind.Newline) && not (BlockSyntax.isBlockEnd reader endKind None) && not reader.IsEof
                    else not (reader.IsAt TokenKind.Newline) && not reader.IsEof

                while inlineContinue () do
                    let key = reader.ReadPropertyKey(allowQuoted=true)
                    reader.Expect TokenKind.Eq
                    if key.Equals("colors", StringComparison.OrdinalIgnoreCase) then
                        values.[key] <- readColorsProperty reader constants
                    else
                        let operand = readColorOperand reader
                        values.[key] <- PaletteColorResolver.resolveOperand operand constants $"palette entry '{key}'"
                reader.SkipNewlines()

        if not (String.IsNullOrWhiteSpace endKind) then BlockSyntax.expectBlockEnd reader endKind None
        values

    let parsePaletteFile (text: string) : PaletteDocument =
        if String.IsNullOrWhiteSpace text then invalidArg "text" "Palette text is required."
        let reader = ParserUtilities.createReader text
        reader.SkipFileDirectives()
        reader.Expect TokenKind.At
        reader.ExpectKeyword "palette"
        let id = reader.ReadIdent()
        if String.IsNullOrWhiteSpace id then raise (DashSpecParseException("Palette module requires @palette <id>."))
        reader.SkipNewlines()
        let constants = parseConstants reader

        let props =
            if reader.TryKeyword "palette" then
                if reader.IsOnNewline() then
                    BlockSyntax.beginBlock reader
                    reader.SkipNewlines()
                    parsePaletteMappings reader constants false "palette"
                elif reader.IsAt TokenKind.LBrace then
                    reader.Expect TokenKind.LBrace
                    reader.SkipNewlines()
                    let values = parsePaletteMappings reader constants true ""
                    reader.Expect TokenKind.RBrace
                    values
                else
                    parsePaletteMappings reader constants false ""
            else
                parsePaletteMappings reader constants false ""

        if props.Count = 0 then raise (DashSpecParseException($"Palette '{id}' requires at least one mapping entry."))
        { Id = id; Properties = props :> IReadOnlyDictionary<_, _> }

    let parsePaletteFileWithId (text: string) =
        let doc = parsePaletteFile text
        doc.Id, doc
