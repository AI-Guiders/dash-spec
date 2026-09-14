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

    let private readTypedValue (reader: TokenReader) (valueType: PropertyValueType) =
        match valueType with
        | PropertyValueType.Scalar -> reader.ReadScalarValue()
        | PropertyValueType.String -> reader.ReadString()
        | PropertyValueType.DateRange -> reader.ReadDateDefaultValue()
        | PropertyValueType.QualifiedName -> reader.ReadQualifiedName()
        | PropertyValueType.CommaList -> reader.ReadCommaSeparatedValues()
        | PropertyValueType.RestOfLine -> reader.ReadRestOfLine()
        | PropertyValueType.ColumnBinding -> invalidOp "ColumnBinding must be handled separately."

    let private writeColumnBinding (values: Dictionary<string, string>) key (binding: ColumnBindingValue) =
        values.[key] <- binding.Column
        match binding.Alias with
        | Some alias -> values.[key + "_as"] <- alias
        | None -> ()

    let private readPropertyEntry (reader: TokenReader) (specs: IDictionary<string, PropertySpec>) allowExtensionProperties allowQuotedKeys blockName (values: Dictionary<string, string>) =
        let key = reader.ReadPropertyKey(allowQuoted=allowQuotedKeys)
        match specs.TryGetValue key with
        | true, spec ->
            if String.Equals(key, "use", StringComparison.OrdinalIgnoreCase)
               && not (reader.IsAt TokenKind.Eq)
               && reader.TryPeekIdent().IsSome then
                values.[key] <- reader.ReadIdent()
            else
                reader.Expect TokenKind.Eq
                if spec.ValueType = PropertyValueType.ColumnBinding then
                    writeColumnBinding values key (reader.ReadColumnBinding())
                else
                    values.[key] <- readTypedValue reader spec.ValueType
        | false, _ ->
            if not allowExtensionProperties || key.EndsWith("_as", StringComparison.OrdinalIgnoreCase) then
                raise (DashSpecParseException($"Unknown property '{key}' in {blockName} block."))
            reader.Expect TokenKind.Eq
            values.[key] <- reader.ReadScalarValue()

    let parseWithEndKind (reader: TokenReader) (schema: PropertySpec list) blockName endKind allowExtensionProperties allowQuotedKeys =
        let specs =
            schema
            |> List.map (fun s -> s.Name, s)
            |> dict

        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let values = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

        while not (BlockSyntax.isBlockEnd reader endKind None) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader endKind None then ()
            else
                while not (reader.IsOnNewline()) && not (BlockSyntax.isBlockEnd reader endKind None) && not reader.IsEof do
                    readPropertyEntry reader specs allowExtensionProperties allowQuotedKeys blockName values
                reader.SkipNewlines()

        BlockSyntax.expectBlockEnd reader endKind None
        values

    let parse (reader: TokenReader) (schema: PropertySpec list) blockName allowExtensionProperties allowQuotedKeys =
        let endKind = resolveEndKind blockName
        parseWithEndKind reader schema blockName endKind allowExtensionProperties allowQuotedKeys

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
                    readPropertyEntry reader specs allowExtensionProperties allowQuotedKeys context values
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
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let values = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

        while not (BlockSyntax.isBlockEnd reader endKind None) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader endKind None then ()
            else
                let key = reader.ReadIdent()
                reader.Expect TokenKind.Eq
                let value = reader.ReadString()
                if values.ContainsKey key then
                    raise (DashSpecParseException($"{blockName}: duplicate key '{key}'."))
                values.[key] <- value
                reader.SkipNewlines()

        BlockSyntax.expectBlockEnd reader endKind None

        if values.Count = 0 then
            raise (DashSpecParseException($"{blockName} requires at least one entry."))

        values

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
