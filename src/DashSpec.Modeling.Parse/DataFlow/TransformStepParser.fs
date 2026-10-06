namespace DashSpec.Modeling.Parse.DataFlow

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Lexing

module TransformStepParser =

    let private readZoneParameterValue (reader: TokenReader) =
        reader.SkipNewlines()

        match reader.CurrentKind with
        | TokenKind.TimeShift -> reader.ReadTimeShift()
        | TokenKind.String -> reader.ReadString()
        | TokenKind.Ident ->
            raise (
                DashSpecParseException(
                    "zone requires a UTC offset literal (TimeShift), e.g. UTC+3 — not a named time zone identifier."))
        | _ -> raise (reader.Unexpected "UTC offset literal (TimeShift)")

    let private readAssignmentValue (reader: TokenReader) =
        reader.SkipNewlines()

        let first =
            match reader.CurrentKind with
            | TokenKind.String -> reader.ReadString()
            | TokenKind.TimeShift -> reader.ReadTimeShift()
            | TokenKind.Ident -> reader.ReadIdent()
            | _ -> raise (reader.Unexpected "assignment value")

        let sb = System.Text.StringBuilder(first)

        while not (reader.IsOnNewline()) && reader.RawKind = TokenKind.Slash do
            reader.Advance()
            sb.Append('/') |> ignore
            ignore (sb.Append(reader.ReadIdentSameLine()))

        sb.ToString()

    let private tryReadIntAssignment (reader: TokenReader) (key: string) =
        reader.SkipNewlines()

        if not (reader.TryKeywordSameLine key) then
            None
        else
            reader.Expect TokenKind.Eq
            let raw = readAssignmentValue reader

            match Int32.TryParse raw with
            | true, value -> Some value
            | _ -> raise (DashSpecParseException($"{key} requires an integer literal."))

    let private validateToZoneParams (transformerId: string) (parameters: DashflowTransformParam[]) =
        let zone =
            parameters
            |> Array.tryFind (fun p -> String.Equals(p.Key, "zone", StringComparison.OrdinalIgnoreCase))
            |> Option.map (fun p -> p.Value)

        let offsetMinutes =
            parameters
            |> Array.tryFind (fun p -> String.Equals(p.Key, "offset_minutes", StringComparison.OrdinalIgnoreCase))
            |> Option.bind (fun p ->
                match Int32.TryParse p.Value with
                | true, v -> Some v
                | _ ->
                    raise (
                        DashSpecParseException(
                            $"transformer '{transformerId}': offset_minutes requires an integer literal.")))

        match zone, offsetMinutes with
        | None, None ->
            raise (
                DashSpecParseException(
                    $"transformer '{transformerId}': transform use to_zone requires zone = UTC±… or offset_minutes = <integer>."))
        | Some z, Some _ ->
            raise (
                DashSpecParseException(
                    $"transformer '{transformerId}': use either zone or offset_minutes for to_zone, not both."))
        | Some z, None ->
            match UtcOffsetZoneLiteral.tryParse z with
            | Result.Ok parsed ->
                [| { Key = "zone"; Value = z }
                   { Key = "offset_minutes"; Value = string parsed.TotalMinutes } |]
            | Result.Error message ->
                raise (DashSpecParseException($"transformer '{transformerId}': {message}"))
        | None, Some minutes ->
            match UtcOffsetZoneLiteral.tryParseOffsetMinutes minutes with
            | Result.Ok parsed ->
                [| { Key = "offset_minutes"; Value = string parsed.TotalMinutes } |]
            | Result.Error message ->
                raise (DashSpecParseException($"transformer '{transformerId}': {message}"))

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

                    let value =
                        if String.Equals(key, "zone", StringComparison.OrdinalIgnoreCase) then
                            readZoneParameterValue reader
                        else
                            readAssignmentValue reader

                    parameters.Add({ Key = key; Value = value })

            reader.Expect TokenKind.RBrace

            let normalized =
                if String.Equals(pluginId, "to_zone", StringComparison.OrdinalIgnoreCase) then
                    validateToZoneParams transformerId (parameters.ToArray())
                else
                    parameters.ToArray()

            { PluginId = pluginId; Parameters = normalized }
        else
            if String.Equals(pluginId, "to_zone", StringComparison.OrdinalIgnoreCase) then
                raise (
                    DashSpecParseException(
                        $"transformer '{transformerId}': transform use to_zone requires a parameter block (zone = UTC±…)."))

            { PluginId = pluginId; Parameters = [||] }
