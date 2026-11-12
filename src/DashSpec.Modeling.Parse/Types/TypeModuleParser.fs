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

    /// Project RowTypeDef[] from a parsed syntax tree (`DashSpecAstNode`), raising
    /// `DashSpecParseException` on any malformed member, missing `end type`, or empty type block.
    let projectTypesFromAst (root: DashSpecAstNode) : RowTypeDef[] =
        let containerMembers =
            match root with
            | DashSpecAstNode.CompilationUnit cu ->
                let hasModule =
                    cu.Members
                    |> Array.exists (function DashSpecAstNode.ModuleDeclaration _ -> true | _ -> false)
                if hasModule then
                    (UnitGrammar.unwrap DashSpecModuleKind.Types root).Body
                else
                    cu.Members
            | _ -> DashSpecAst.members root

        let defs = ResizeArray<RowTypeDef>()

        for node in containerMembers do
            match node with
            | DashSpecAstNode.BlockDeclaration block ->
                match block.Opener with
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Type, typeName) ->
                    let hasEnd =
                        block.Members
                        |> Array.exists (function
                            | DashSpecAstNode.EndBlock e when e.EndKeyword = DashSpecBlockKeyword.Type ->
                                match e.EndId with
                                | Some endId when not (String.Equals(endId, typeName, StringComparison.Ordinal)) ->
                                    raise (DashSpecParseException($"end type '{endId}' does not match type '{typeName}'."))
                                | _ -> true
                            | DashSpecAstNode.EndBlock e ->
                                raise (DashSpecParseException($"type '{typeName}': unexpected 'end {DashSpecBlockKeyword.toEndName e.EndKeyword}'."))
                            | _ -> false)

                    if not hasEnd then
                        raise (DashSpecParseException($"type '{typeName}' requires 'end type'."))

                    let fields = ResizeArray<RowFieldDef>()

                    for m in block.Members do
                        match m with
                        | DashSpecAstNode.TypeFieldDeclaration f ->
                            fields.Add
                                { Name = f.FieldName
                                  Type = f.FieldType
                                  Optional = f.Optional }
                        | DashSpecAstNode.BlankLine _
                        | DashSpecAstNode.EndBlock _ -> ()
                        | m when DashSpecAst.isTrivia m -> ()
                        | other ->
                            let lineText = DashSpecAst.tokens other |> Array.map (fun t -> t.Text) |> String.concat " "
                            raise (DashSpecParseException($"type '{typeName}': invalid field declaration '{lineText}'."))

                    if fields.Count = 0 then
                        raise (DashSpecParseException($"type '{typeName}' must declare at least one field."))

                    defs.Add { Name = typeName; Fields = fields.ToArray() }
                | _ ->
                    let headerText = block.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                    raise (DashSpecParseException($"Unexpected block '{headerText}' in types module."))
            | DashSpecAstNode.ClauseDeclaration c ->
                match c.Kind with
                | ClauseKind.NamespaceClause _ -> ()
                | _ ->
                    let lineText = c.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                    raise (DashSpecParseException($"Unexpected directive '{lineText}' in types module."))
            | DashSpecAstNode.ImportDeclaration _
            | DashSpecAstNode.BlankLine _
            | DashSpecAstNode.EndBlock _ -> ()
            | n when DashSpecAst.isTrivia n -> ()
            | other ->
                let lineText = DashSpecAst.tokens other |> Array.map (fun t -> t.Text) |> String.concat " "
                raise (DashSpecParseException($"Unexpected statement '{lineText}' in types module."))

        defs.ToArray()

    /// Compatibility wrapper returning `Some` over `projectTypesFromAst`.
    let tryProjectTypesFromAst (root: DashSpecAstNode) : RowTypeDef[] option =
        try
            let defs = projectTypesFromAst root
            if defs.Length > 0 then Some defs else None
        with :? DashSpecParseException -> None

    let parseTypesModule (text: string) =
        if String.IsNullOrWhiteSpace text then invalidArg "text" "Types text is required."

        let tree = DashSpecSurfaceSyntax.parse text
        let defs = projectTypesFromAst tree.Root

        let catalog = TypeCatalog.ofDefinitions defs

        match TypeCatalog.validate catalog with
        | Result.Ok () -> ()
        | Result.Error errors -> raise (DashSpecParseException(String.Join("; ", errors)))

        defs
