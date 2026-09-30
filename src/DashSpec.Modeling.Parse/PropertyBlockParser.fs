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
