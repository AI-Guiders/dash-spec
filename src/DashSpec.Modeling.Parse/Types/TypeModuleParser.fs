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

    let private parseTypeString (typeName: string) (rawType: string) : bool * DashType =
        let parts = rawType.Split(' ', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries)
        if parts.Length = 0 then
            raise (DashSpecParseException($"type '{typeName}': field type is required."))
        let optional, rem =
            if String.Equals(parts.[0], "optional", StringComparison.OrdinalIgnoreCase) then
                true, parts.[1..]
            elif parts.[0].StartsWith('?') && parts.[0].Length > 1 then
                true, Array.append [| parts.[0].Substring(1) |] parts.[1..]
            else
                false, parts
        if rem.Length = 0 then
            raise (DashSpecParseException($"type '{typeName}': field type is required."))
        elif String.Equals(rem.[0], "array", StringComparison.OrdinalIgnoreCase) then
            if rem.Length < 3 then
                raise (DashSpecParseException($"type '{typeName}': array requires element type and length."))
            match DashPrimitive.tryParseFieldKeyword rem.[1], Int32.TryParse rem.[2] with
            | Some elem, (true, len) when len > 0 ->
                optional, DashType.FixedArray(elem, len)
            | None, _ ->
                raise (DashSpecParseException($"array element must be a primitive, not '{rem.[1]}'."))
            | _ ->
                raise (DashSpecParseException("array requires a fixed positive integer length."))
        else
            let tName = rem.[0]
            let dt =
                match DashPrimitive.tryParseFieldKeyword tName with
                | Some prim -> DashType.Primitive prim
                | None -> DashType.Named tName
            optional, dt

    let private extractTrailingFieldMetadata (tokens: SyntaxToken[]) : string option * string option =
        let mutable format: string option = None
        let mutable label: string option = None
        let mutable i = 0
        while i < tokens.Length do
            if i + 1 < tokens.Length
               && tokens.[i].LexKind = TokenKind.Ident
               && String.Equals(tokens.[i].Text, "as", StringComparison.OrdinalIgnoreCase)
               && tokens.[i + 1].LexKind = TokenKind.String then
                label <- Some(Construct.unquote tokens.[i + 1].Text)
                i <- i + 2
            elif i + 2 < tokens.Length
                 && tokens.[i].LexKind = TokenKind.Ident
                 && tokens.[i + 1].LexKind = TokenKind.Eq then
                let k = tokens.[i].Text
                let v = Construct.unquote tokens.[i + 2].Text
                if String.Equals(k, "format", StringComparison.OrdinalIgnoreCase) then
                    format <- Some v
                elif String.Equals(k, "label", StringComparison.OrdinalIgnoreCase) then
                    label <- Some v
                i <- i + 3
            else
                i <- i + 1
        format, label

    let private typeTableDeclaration (typeName: string) (addField: RowFieldDef -> unit) : Construct.Declaration<RowFieldDef> =
        { Name = $"type '{typeName}'"
          Vocabulary = [ "name"; "id"; "field"; "type"; "optional"; "format"; "label"; "title" ]
          CellsToRow = fun m ->
              let fieldName =
                  Construct.tryCell m "name"
                  |> Option.orElse (Construct.tryCell m "id")
                  |> Option.orElse (Construct.tryCell m "field")
                  |> Option.defaultWith (fun () ->
                      raise (DashSpecMissingMemberException("name", $"type '{typeName}' table row requires a 'name' (or 'id'/'field') cell.")))
              let rawType =
                  Construct.tryCell m "type"
                  |> Option.defaultWith (fun () ->
                      raise (DashSpecMissingMemberException("type", $"type '{typeName}' field '{fieldName}' requires a 'type' cell.")))
              let optFromType, dashType = parseTypeString typeName rawType
              let optFromCol = Construct.tryCell m "optional" |> Construct.tryParseBool |> Option.defaultValue false
              let fmt = Construct.tryCell m "format" |> Option.map Construct.unquote
              let lbl =
                  Construct.tryCell m "label"
                  |> Option.orElse (Construct.tryCell m "title")
                  |> Option.map Construct.unquote
              { Name = fieldName
                Type = dashType
                Optional = optFromType || optFromCol }
              |> RowFieldDef.withMetadata fmt lbl
          ConstructRow = addField }

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

                    let tryProjectFieldLine (m: DashSpecAstNode) : (string * string) seq option =
                        let appendMeta (fmt: string option) (lbl: string option) (basePairs: (string * string) list) =
                            seq {
                                yield! basePairs
                                match fmt with Some f -> yield "format", f | None -> ()
                                match lbl with Some l -> yield "label", l | None -> ()
                            }

                        match m with
                        | DashSpecAstNode.TypeFieldDeclaration f ->
                            let fmt, lbl = extractTrailingFieldMetadata f.Tokens
                            Some(
                                appendMeta fmt lbl
                                    [ "name", f.FieldName
                                      "type", DashType.describe f.FieldType
                                      "optional", (if f.Optional then "true" else "false") ])
                        | DashSpecAstNode.PropertyAssignment p ->
                            let rawVal, lblFromAlias =
                                match p.Value with
                                | PropertyValueSyntax.Aliased(inner, title) -> inner.Text, Some title
                                | _ -> p.ValueText, None
                            let fmt, lblFromToks = extractTrailingFieldMetadata p.ValueTokens
                            Some(
                                appendMeta fmt (lblFromAlias |> Option.orElse lblFromToks)
                                    [ "name", p.Key
                                      "type", rawVal ])
                        | DashSpecAstNode.TableRowDeclaration _
                        | DashSpecAstNode.BlankLine _
                        | DashSpecAstNode.EndBlock _ -> None
                        | m when DashSpecAst.isTrivia m -> None
                        | other ->
                            let toks = DashSpecAst.tokens other
                            if toks.Length >= 3
                               && toks.[0].LexKind = TokenKind.Ident
                               && toks.[1].LexKind = TokenKind.Eq then
                                let fieldName = toks.[0].Text
                                let valueToks = toks.[2..]
                                let typeToks =
                                    valueToks
                                    |> Array.takeWhile (fun t ->
                                        not (t.LexKind = TokenKind.Ident
                                             && (String.Equals(t.Text, "as", StringComparison.OrdinalIgnoreCase)
                                                 || String.Equals(t.Text, "format", StringComparison.OrdinalIgnoreCase)
                                                 || String.Equals(t.Text, "label", StringComparison.OrdinalIgnoreCase))))
                                let rawType = typeToks |> Array.map (fun t -> t.Text) |> String.concat " "
                                let fmt, lbl = extractTrailingFieldMetadata valueToks
                                Some(
                                    appendMeta fmt lbl
                                        [ "name", fieldName
                                          "type", rawType ])
                            else
                                let lineText = toks |> Array.map (fun t -> t.Text) |> String.concat " "
                                raise (DashSpecParseException($"type '{typeName}': invalid field declaration '{lineText}'."))

                    Construct.projectAstWithLines
                        (typeTableDeclaration typeName fields.Add)
                        None
                        "name"
                        (Some tryProjectFieldLine)
                        block.Members

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
