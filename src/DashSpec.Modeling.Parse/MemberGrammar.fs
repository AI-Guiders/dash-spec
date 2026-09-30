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

    let private parseStringMapBodyCore
        (reader: TokenReader)
        endKind
        blockName
        (readValue: TokenReader -> string)
        (endId: string option)
        (requireAtLeastOne: bool)
        =
        let values = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

        while not (BlockSyntax.isBlockEnd reader endKind endId) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader endKind endId then ()
            else
                let key = reader.ReadIdent()
                if String.IsNullOrWhiteSpace key then
                    raise (DashSpecParseException($"{blockName}: entry name is required."))
                reader.Expect TokenKind.Eq
                let value = readValue reader
                if String.IsNullOrWhiteSpace value then
                    raise (DashSpecParseException($"{blockName}: value for '{key}' is required."))
                if values.ContainsKey key then
                    raise (DashSpecParseException($"{blockName}: duplicate key '{key}'."))
                values.[key] <- value.Trim()
                reader.SkipNewlines()

        if requireAtLeastOne && values.Count = 0 then
            raise (DashSpecParseException($"{blockName} requires at least one entry."))

        values

    /// `ident = <value>` entries until `end <endKind>` (body only; caller opened the block).
    let parseStringMapBody
        (reader: TokenReader)
        endKind
        blockName
        (readValue: TokenReader -> string)
        =
        parseStringMapBodyCore reader endKind blockName readValue None true

    /// `ident = <value>` block with `beginBlock` / `expectBlockEnd`.
    let parseStringMapBlock
        (reader: TokenReader)
        endKind
        blockName
        (readValue: TokenReader -> string)
        =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let values = parseStringMapBodyCore reader endKind blockName readValue None true
        BlockSyntax.expectBlockEnd reader endKind None
        values

    /// `ident = ident` map (commands, tooltip variables).
    let parseIdentMapBlock (reader: TokenReader) endKind blockName =
        parseStringMapBlock reader endKind blockName (fun r -> r.ReadIdent())

    let parsePresetRestOfLine (reader: TokenReader) = reader.ReadRestOfLine().Trim()

    /// Layout board cell: `card_id` or `card_id:weight`.
    let readBoardCell (reader: TokenReader) =
        let id = reader.ReadIdent()
        if reader.IsAt TokenKind.Colon then
            reader.Expect TokenKind.Colon
            let weight = reader.ReadIdent()
            $"{id}:{weight}"
        else
            id

    /// Layout row: `[ cell … ]` (ADR-0020 / ADR-0071).
    let parseBoardRow (reader: TokenReader) =
        reader.Expect TokenKind.LBracket
        reader.SkipNewlines()
        let cells = ResizeArray<string>()

        while not (reader.IsAt TokenKind.RBracket) && not reader.IsEof do
            reader.SkipNewlines()
            if reader.IsAt TokenKind.RBracket then ()
            else
                cells.Add(readBoardCell reader)
                reader.SkipNewlines()

        reader.Expect TokenKind.RBracket

        if cells.Count = 0 then
            raise (DashSpecParseException("Layout board row [ … ] must list at least one card ref or id."))

        cells :> IReadOnlyList<string>
