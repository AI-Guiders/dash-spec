namespace DashSpec.Modeling.Parse

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Lexing

/// Minimal schema-driven property blocks for fragment parsers (ADR-0048 M5).
module PropertyBlockParser =

    type PropertyValueType =
        | Scalar
        | String

    type PropertySpec = { Name: string; ValueType: PropertyValueType }

    let seriesTransformSchema: PropertySpec list =
        [ { Name = "use"; ValueType = Scalar }
          { Name = "max"; ValueType = Scalar }
          { Name = "other"; ValueType = String } ]

    let private readTypedValue (reader: TokenReader) valueType =
        match valueType with
        | Scalar -> reader.ReadScalarValue()
        | String -> reader.ReadString()

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
                values.[key] <- readTypedValue reader spec.ValueType
        | false, _ ->
            if not allowExtensionProperties then
                raise (DashSpecParseException($"Unknown property '{key}' in {blockName} block."))
            reader.Expect TokenKind.Eq
            values.[key] <- reader.ReadScalarValue()

    let parse (reader: TokenReader) (schema: PropertySpec list) blockName endKind allowExtensionProperties allowQuotedKeys =
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
