namespace DashSpec.Modeling.Parse.Types

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module TypeModuleParser =

    let private parsePrimitive (reader: TokenReader) =
        let name = reader.ReadIdent()
        match name.ToLowerInvariant() with
        | "bool" -> DashPrimitive.Bool
        | "int" -> DashPrimitive.Int
        | "decimal" -> DashPrimitive.Decimal
        | "string" -> DashPrimitive.String
        | "duration" -> DashPrimitive.Duration
        | "date" -> DashPrimitive.Date
        | "time" -> DashPrimitive.Time
        | "datetime" -> DashPrimitive.DateTime
        | other -> raise (DashSpecParseException($"Unknown primitive type '{other}'."))

    let private parseFieldLine (reader: TokenReader) =
        let mutable optional = false
        if reader.TryKeyword "optional" then
            optional <- true
        let kind = parsePrimitive reader
        let fieldName = reader.ReadIdent()
        if String.IsNullOrWhiteSpace fieldName then
            raise (DashSpecParseException("Row field requires a name."))
        { Name = fieldName; Kind = kind; Optional = optional }

    let parseTypeBlockAfterKeyword (reader: TokenReader) =
        let typeName = reader.ReadIdent()
        if String.IsNullOrWhiteSpace typeName then
            raise (DashSpecParseException("type requires a name."))
        reader.SkipNewlines()
        let fields = ResizeArray<RowFieldDef>()

        while not (BlockSyntax.isBlockEnd reader "type" (Some typeName)) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "type" (Some typeName) then ()
            else fields.Add(parseFieldLine reader)

        BlockSyntax.expectBlockEnd reader "type" (Some typeName)

        if fields.Count = 0 then
            raise (DashSpecParseException($"type '{typeName}' must declare at least one field."))

        { Name = typeName; Fields = fields.ToArray() }

    let parseTypeBlock (reader: TokenReader) =
        reader.ExpectKeyword "type"
        parseTypeBlockAfterKeyword reader

    let parseTypesModule (text: string) =
        if String.IsNullOrWhiteSpace text then invalidArg "text" "Types text is required."
        let reader = ParserUtilities.createReader text
        reader.SkipFileDirectives()
        reader.SkipNewlines()

        if reader.IsAt TokenKind.At then
            reader.Advance()
            if reader.TryKeyword "types" then
                reader.ReadIdent() |> ignore
                reader.SkipNewlines()

        let defs = ResizeArray<RowTypeDef>()

        while not reader.IsEof do
            reader.SkipNewlines()
            if not reader.IsEof then
                defs.Add(parseTypeBlock reader)

        defs.ToArray()
