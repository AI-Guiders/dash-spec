namespace DashSpec.Modeling.Parse.Types

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Syntax

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

            // qualified field names ride the dot (`r.col`, ADR-0128 composite surface) —
            // the same dotted-ident rule as property keys, one language
            let fieldName = reader.ReadDottedPropertyKey()

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

    /// Project RowTypeDef[] from a parsed syntax tree (`DashSpecAstNode`), returns None if
    /// any type block has malformed/unrecognized member lines or missing end block so the
    /// caller can report exact syntax diagnostics.
    let tryProjectTypesFromAst (root: DashSpecAstNode) : RowTypeDef[] option =
        let typeBlocks =
            DashSpecAst.descendants root
            |> Seq.choose (function
                | DashSpecAstNode.BlockDeclaration block ->
                    match block.Opener with
                    | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Type, typeName) -> Some(typeName, block)
                    | _ -> None
                | _ -> None)
            |> Seq.toArray

        if typeBlocks.Length = 0 then
            None
        else
            let defs = ResizeArray<RowTypeDef>(typeBlocks.Length)
            let mutable ok = true

            for typeName, block in typeBlocks do
                let hasEnd =
                    block.Members
                    |> Array.exists (function
                        | DashSpecAstNode.EndBlock e when e.EndKeyword = DashSpecBlockKeyword.Type -> true
                        | _ -> false)

                let hasUnrecognizedLine =
                    block.Members
                    |> Array.exists (fun m ->
                        match m with
                        | DashSpecAstNode.TypeFieldDeclaration _
                        | DashSpecAstNode.BlankLine _
                        | DashSpecAstNode.EndBlock _ -> false
                        | m when DashSpecAst.isTrivia m -> false
                        | _ -> true)

                let fields =
                    block.Members
                    |> Array.choose (function
                        | DashSpecAstNode.TypeFieldDeclaration f ->
                            Some
                                { Name = f.FieldName
                                  Type = f.FieldType
                                  Optional = f.Optional }
                        | _ -> None)

                if not hasEnd || hasUnrecognizedLine || fields.Length = 0 then
                    ok <- false
                else
                    defs.Add { Name = typeName; Fields = fields }

            if ok then Some(defs.ToArray()) else None

    let parseTypesModule (text: string) =
        if String.IsNullOrWhiteSpace text then invalidArg "text" "Types text is required."

        let tree = DashSpecSurfaceSyntax.parse text

        let defs =
            match tryProjectTypesFromAst tree.Root with
            | Some projected -> projected
            | None ->
                let reader = ParserUtilities.createReader text
                reader.SkipFileDirectives()
                reader.SkipNewlines()

                if reader.TryKeyword "module" then
                    if reader.TryKeyword "types" then
                        reader.ReadIdent() |> ignore
                        reader.SkipNewlines()

                        if reader.TryKeyword "namespace" then
                            reader.ReadDottedPropertyKey() |> ignore
                            reader.SkipNewlines()

                let parsed = ResizeArray<RowTypeDef>()

                while not reader.IsEof do
                    reader.SkipNewlines()

                    if not reader.IsEof then
                        parsed.Add(parseTypeBlock reader)

                parsed.ToArray()

        let catalog = TypeCatalog.ofDefinitions defs

        match TypeCatalog.validate catalog with
        | Result.Ok () -> ()
        | Result.Error errors -> raise (DashSpecParseException(String.Join("; ", errors)))

        defs
