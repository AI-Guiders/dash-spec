namespace DashSpec.Modeling.Parse

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Lexing

/// Schema-driven `key = value` lines inside blocks (ADR-0071).
module MemberGrammar =

    type PropertyValueType = PropertySchemas.PropertyValueType
    type PropertySpec = PropertySchemas.PropertySpec

    let readTypedValue (reader: TokenReader) (valueType: PropertyValueType) =
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

    let readPropertyEntry
        (reader: TokenReader)
        (specs: IDictionary<string, PropertySpec>)
        allowExtensionProperties
        allowQuotedKeys
        blockName
        (values: Dictionary<string, string>)
        =
        let key = reader.ReadPropertyKey(allowQuoted=allowQuotedKeys)
        match specs.TryGetValue key with
        | true, (spec: PropertySpec) ->
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

    /// `ident = <rest-of-line>` entries until `end <endKind>`.
    let parseStringMapBlock
        (reader: TokenReader)
        endKind
        blockName
        (readValue: TokenReader -> string)
        =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let values = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

        while not (BlockSyntax.isBlockEnd reader endKind None) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader endKind None then ()
            else
                let key = reader.ReadIdent()
                reader.Expect TokenKind.Eq
                let value = readValue reader
                if String.IsNullOrWhiteSpace value then
                    raise (DashSpecParseException($"{blockName}: value for '{key}' is required."))
                if values.ContainsKey key then
                    raise (DashSpecParseException($"{blockName}: duplicate key '{key}'."))
                values.[key] <- value.Trim()
                reader.SkipNewlines()

        BlockSyntax.expectBlockEnd reader endKind None

        if values.Count = 0 then
            raise (DashSpecParseException($"{blockName} requires at least one entry."))

        values

    let parsePresetRestOfLine (reader: TokenReader) = reader.ReadRestOfLine().Trim()
