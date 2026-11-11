namespace DashSpec.Modeling.Parse

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Lexing

/// Schema-driven property blocks for fragment parsers (ADR-0048).
module PropertyBlockParser =

    type PropertyValueType = PropertySchemas.PropertyValueType
    type PropertySpec = PropertySchemas.PropertySpec

    let seriesTransformSchema = PropertySchemas.seriesTransform

    let resolveEndKind blockName = PropertySchemas.resolveEndKind blockName

    let readPropertyEntry =
        MemberGrammar.readPropertyEntry

    let parseWithEndKind
        (reader: TokenReader)
        (schema: PropertySpec list)
        blockName
        endKind
        allowExtensionProperties
        allowQuotedKeys
        (endId: string option)
        : Dictionary<string, string>
        =
        BlockGrammar.parseKeywordContainer
            reader
            endKind
            blockName
            [ BlockGrammar.SchemaProperties(schema, allowExtensionProperties, allowQuotedKeys) ]
            endId

    let parse (reader: TokenReader) (schema: PropertySpec list) blockName allowExtensionProperties allowQuotedKeys =
        let endKind = resolveEndKind blockName
        parseWithEndKind reader schema blockName endKind allowExtensionProperties allowQuotedKeys None

    let private unwrapQuotes (raw: string) =
        let trimmed = raw.Trim()
        if trimmed.Length >= 2 && trimmed.[0] = '"' && trimmed.[trimmed.Length - 1] = '"' then
            trimmed.Substring(1, trimmed.Length - 2)
        else
            trimmed

    /// <summary>ADR-0130: project property values from typed
    /// PropertyAssignment and `use` ClauseDeclaration members — one pass over the tree,
    /// raising `DashSpecParseException` on unknown keys or invalid member lines.</summary>
    let projectPropertiesFromAst
        (members: DashSpec.Modeling.Parse.Syntax.DashSpecAstNode[])
        (schema: PropertySpec list)
        (blockName: string)
        allowExtensionProperties
        : Dictionary<string, string> =
        let specs = Dictionary<string, PropertySpec>(StringComparer.OrdinalIgnoreCase)
        for s in schema do
            specs.[s.Name] <- s

        let values = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

        for memberNode in members do
            match memberNode with
            | DashSpec.Modeling.Parse.Syntax.DashSpecAstNode.PropertyAssignment p ->
                match specs.TryGetValue p.Key with
                | true, spec ->
                    match spec.ValueType with
                    | PropertyValueType.String ->
                        if p.ValueTokens.Length = 1 && p.ValueTokens.[0].LexKind = TokenKind.String then
                            values.[p.Key] <- unwrapQuotes p.ValueText
                        else
                            raise (DashSpecParseException($"Property '{p.Key}' in {blockName} requires a string literal."))
                    | PropertyValueType.Scalar ->
                        if p.ValueTokens.Length = 1 then
                            values.[p.Key] <- unwrapQuotes p.ValueText
                        else
                            raise (DashSpecParseException($"Property '{p.Key}' in {blockName} requires a scalar value."))
                    | PropertyValueType.CommaList ->
                        values.[p.Key] <- p.ValueText.Replace(" , ", ", ", StringComparison.Ordinal)
                    | PropertyValueType.RestOfLine
                    | PropertyValueType.DateRange
                    | PropertyValueType.QualifiedName ->
                        values.[p.Key] <- unwrapQuotes p.ValueText
                    | PropertyValueType.ColumnBinding ->
                        values.[p.Key] <- unwrapQuotes p.ValueText
                | false, _ ->
                    if allowExtensionProperties && not (p.Key.EndsWith("_as", StringComparison.OrdinalIgnoreCase)) then
                        values.[p.Key] <- unwrapQuotes p.ValueText
                    else
                        raise (DashSpecParseException($"Unknown property '{p.Key}' in {blockName} block."))
            | DashSpec.Modeling.Parse.Syntax.DashSpecAstNode.ClauseDeclaration c ->
                match c.Kind with
                | DashSpec.Modeling.Parse.Syntax.ClauseKind.NamespaceClause _ -> ()
                | DashSpec.Modeling.Parse.Syntax.ClauseKind.DirectiveClause("use", [| preset |]) when specs.ContainsKey "use" ->
                    values.["use"] <- preset
                | _ ->
                    let lineText = c.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                    raise (DashSpecParseException($"Unexpected directive '{lineText}' in {blockName} block."))
            | DashSpec.Modeling.Parse.Syntax.DashSpecAstNode.ImportDeclaration _
            | DashSpec.Modeling.Parse.Syntax.DashSpecAstNode.BlankLine _
            | DashSpec.Modeling.Parse.Syntax.DashSpecAstNode.EndBlock _ -> ()
            | memberNode when DashSpec.Modeling.Parse.Syntax.DashSpecAst.isTrivia memberNode -> ()
            | other ->
                let lineText = DashSpec.Modeling.Parse.Syntax.DashSpecAst.tokens other |> Array.map (fun t -> t.Text) |> String.concat " "
                raise (DashSpecParseException($"Unexpected statement '{lineText}' in {blockName} block."))

        values

    /// <summary>Compatibility wrapper returning `Some` over `projectPropertiesFromAst`.</summary>
    let tryProjectPropertiesFromAst
        (members: DashSpec.Modeling.Parse.Syntax.DashSpecAstNode[])
        (schema: PropertySpec list)
        allowExtensionProperties
        : Dictionary<string, string> option =
        try
            Some(projectPropertiesFromAst members schema "property" allowExtensionProperties)
        with :? DashSpecParseException -> None

    let parseFlatProperties (reader: TokenReader) (schema: PropertySpec list) context allowExtensionProperties allowQuotedKeys =
        let specs =
            schema
            |> List.map (fun s -> s.Name, s)
            |> dict

        let values = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

        while not reader.IsEof do
            reader.SkipNewlines()
            if reader.IsEof then ()
            else
                while not (reader.IsAt TokenKind.Newline) && not reader.IsEof do
                    MemberGrammar.readPropertyEntry reader specs allowExtensionProperties allowQuotedKeys context values
                reader.SkipNewlines()

        values

    let parseCommaListBlock (reader: TokenReader) endKind blockName =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let names = ResizeArray<string>()

        while not (BlockSyntax.isBlockEnd reader endKind None) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader endKind None then ()
            else
                names.Add(reader.ReadIdent())
                reader.SkipNewlines()
                if reader.CurrentKind = TokenKind.Comma then reader.Advance()

        reader.SkipNewlines()
        BlockSyntax.expectBlockEnd reader endKind None

        if names.Count = 0 then
            raise (DashSpecParseException($"{blockName} block requires at least one name."))

        names :> IReadOnlyList<_>

    let parseIdentListBlock (reader: TokenReader) endKind blockName =
        parseCommaListBlock reader endKind blockName

    let parseStringMapBlock (reader: TokenReader) endKind blockName =
        MemberGrammar.parseStringMapBlock reader endKind blockName (fun r -> r.ReadString())

    let private readTitleToken (reader: TokenReader) =
        match reader.CurrentKind with
        | TokenKind.String -> reader.ReadString()
        | TokenKind.Ident -> reader.ReadIdent()
        | _ -> raise (reader.Unexpected "card title")

    let parseTitleListBlock (reader: TokenReader) endKind blockName =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let titles = ResizeArray<string>()

        while not (BlockSyntax.isBlockEnd reader endKind None) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader endKind None then ()
            else
                titles.Add(readTitleToken reader)
                reader.SkipNewlines()
                if reader.CurrentKind = TokenKind.Comma then reader.Advance()

        reader.SkipNewlines()
        BlockSyntax.expectBlockEnd reader endKind None

        if titles.Count = 0 then
            raise (DashSpecParseException($"{blockName} block requires at least one title."))

        titles :> IReadOnlyList<_>
