namespace DashSpec.Modeling.Parse.Types

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module TypeModuleParser =

    let private parsePrimitive (reader: TokenReader) =
        let name = reader.ReadIdent()

        match DashPrimitive.tryParseFieldKeyword name with
        | Some primitive -> DashType.Primitive primitive
        | None -> raise (DashSpecParseException($"Unknown primitive type '{name}'."))

    let private parseFieldType (reader: TokenReader) =
        if reader.TryKeyword "array" then
            let elementName = reader.ReadIdent()

            match DashPrimitive.tryParseFieldKeyword elementName with
            | None -> raise (DashSpecParseException($"array element must be a primitive, not '{elementName}'."))
            | Some element ->
                let lengthText = reader.ReadIdent()

                match Int32.TryParse lengthText with
                | true, length when length > 0 -> ()
                | _ -> raise (DashSpecParseException("array requires a fixed positive integer length."))

                let length = Int32.Parse lengthText
                let fieldName = reader.ReadIdent()

                if String.IsNullOrWhiteSpace fieldName then
                    raise (DashSpecParseException("array field requires a name."))

                DashType.FixedArray(element, length), fieldName
        else
            let typeName = reader.ReadIdent()

            if String.IsNullOrWhiteSpace typeName then
                raise (DashSpecParseException("Field type is required."))

            let fieldName = reader.ReadIdent()

            if String.IsNullOrWhiteSpace fieldName then
                raise (DashSpecParseException("Row field requires a name."))

            match DashPrimitive.tryParseFieldKeyword typeName with
            | Some primitive -> DashType.Primitive primitive, fieldName
            | None -> DashType.Named typeName, fieldName

    let private parseFieldLine (reader: TokenReader) =
        let mutable optional = false

        if reader.TryKeyword "optional" then
            optional <- true

        let dashType, fieldName = parseFieldType reader
        { Name = fieldName; Type = dashType; Optional = optional }

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

        let catalog = TypeCatalog.ofDefinitions defs

        match TypeCatalog.validate catalog with
        | Result.Ok () -> ()
        | Result.Error errors -> raise (DashSpecParseException(String.Join("; ", errors)))

        defs.ToArray()
