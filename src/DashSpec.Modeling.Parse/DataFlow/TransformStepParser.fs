namespace DashSpec.Modeling.Parse.DataFlow

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Lexing

module TransformStepParser =

    let private readTimeShiftZoneValue (reader: TokenReader) =
        reader.SkipNewlines()

        match reader.CurrentKind with
        | TokenKind.TimeShift -> reader.ReadTimeShift()
        | TokenKind.String -> reader.ReadString()
        | TokenKind.IanaZone ->
            raise (
                DashSpecParseException(
                    "to_zone requires a TimeShift literal (UTC±…); use transform use iana_to_timeshift for IANA zones."))
        | _ -> raise (reader.Unexpected "TimeShift zone literal (e.g. UTC+3)")

    let private readIanaZoneValue (reader: TokenReader) =
        reader.SkipNewlines()

        match reader.CurrentKind with
        | TokenKind.IanaZone -> reader.ReadIanaZone()
        | TokenKind.String -> reader.ReadString()
        | TokenKind.TimeShift ->
            raise (DashSpecParseException("iana_to_timeshift requires an IANA zone literal (e.g. Europe/Moscow)."))
        | _ -> raise (reader.Unexpected "IANA zone literal")

    let private readAssignmentValue (reader: TokenReader) =
        reader.SkipNewlines()

        let first =
            match reader.CurrentKind with
            | TokenKind.String -> reader.ReadString()
            | TokenKind.TimeShift -> reader.ReadTimeShift()
            | TokenKind.IanaZone -> reader.ReadIanaZone()
            | TokenKind.Ident -> reader.ReadIdent()
            | _ -> raise (reader.Unexpected "assignment value")

        let sb = System.Text.StringBuilder(first)

        while not (reader.IsOnNewline()) && reader.RawKind = TokenKind.Slash do
            reader.Advance()
            sb.Append('/') |> ignore
            ignore (sb.Append(reader.ReadIdentSameLine()))

        sb.ToString()

    let private readParameterValue (pluginId: string) (key: string) (reader: TokenReader) =
        if String.Equals(pluginId, "to_zone", StringComparison.OrdinalIgnoreCase)
           && String.Equals(key, "zone", StringComparison.OrdinalIgnoreCase) then
            readTimeShiftZoneValue reader
        elif String.Equals(pluginId, "iana_to_timeshift", StringComparison.OrdinalIgnoreCase)
                 && (String.Equals(key, "iana", StringComparison.OrdinalIgnoreCase)
                     || String.Equals(key, "zone", StringComparison.OrdinalIgnoreCase)) then
            readIanaZoneValue reader
        elif String.Equals(pluginId, "timeshift_to_iana", StringComparison.OrdinalIgnoreCase)
                 && String.Equals(key, "zone", StringComparison.OrdinalIgnoreCase) then
            readTimeShiftZoneValue reader
        else
            readAssignmentValue reader

    let parseTransformUseBlock (reader: TokenReader) (transformerId: string) : DashflowTransformStepDef =
        reader.ExpectKeyword "use"
        let pluginId = reader.ReadIdent()

        if reader.IsAt TokenKind.LBrace then
            reader.Expect TokenKind.LBrace
            let parameters = ResizeArray<DashflowTransformParam>()

            while not (reader.IsAt TokenKind.RBrace) && not reader.IsEof do
                reader.SkipNewlines()

                if reader.IsAt TokenKind.RBrace then ()
                else
                    let key = reader.ReadIdent()
                    reader.Expect TokenKind.Eq
                    let value = readParameterValue pluginId key reader
                    parameters.Add({ Key = key; Value = value })

            reader.Expect TokenKind.RBrace

            let raw =
                parameters.ToArray() |> Array.map (fun p -> p.Key, p.Value)

            let normalized =
                BuiltinScalarTransforms.normalizeStep transformerId pluginId raw
                |> Array.map (fun (k, v) -> { Key = k; Value = v })

            { PluginId = pluginId; Parameters = normalized }
        else
            if String.Equals(pluginId, "to_zone", StringComparison.OrdinalIgnoreCase) then
                raise (
                    DashSpecParseException(
                        $"transformer '{transformerId}': transform use to_zone requires a parameter block (zone = UTC±…)."))

            if String.Equals(pluginId, "iana_to_timeshift", StringComparison.OrdinalIgnoreCase) then
                raise (
                    DashSpecParseException(
                        $"transformer '{transformerId}': transform use iana_to_timeshift requires a parameter block (iana = …)."))

            { PluginId = pluginId; Parameters = [||] }
