namespace DashSpec.Modeling.Core

open System

module BuiltinScalarTransforms =

    let private findParam (parameters: (string * string)[]) key =
        parameters
        |> Array.tryFind (fun (k, _) -> String.Equals(k, key, StringComparison.OrdinalIgnoreCase))
        |> Option.map snd

    let validateIanaToTimeShift (transformerId: string) (parameters: (string * string)[]) =
        let iana =
            findParam parameters "iana"
            |> Option.orElseWith (fun () -> findParam parameters "zone")
            |> Option.defaultWith (fun () ->
                raise (
                    DashSpecParseException(
                        $"transformer '{transformerId}': iana_to_timeshift requires iana = <IanaZone> (or zone = …).")))

        match IanaZoneConverter.tryToTimeShift iana ZoneResolveReference.Utc with
        | Result.Ok parsed -> [| "iana", iana; "offset_minutes", string parsed.TotalMinutes |]
        | Result.Error message ->
            raise (DashSpecParseException($"transformer '{transformerId}': {message}"))

    let validateTimeShiftToIana (transformerId: string) (parameters: (string * string)[]) =
        let minutes =
            match findParam parameters "offset_minutes" with
            | Some raw ->
                match Int32.TryParse raw with
                | true, v ->
                    match UtcOffsetZoneLiteral.tryParseOffsetMinutes v with
                    | Result.Ok parsed -> parsed.TotalMinutes
                    | Result.Error message -> raise (DashSpecParseException($"transformer '{transformerId}': {message}"))
                | _ ->
                    raise (
                        DashSpecParseException(
                            $"transformer '{transformerId}': offset_minutes requires an integer literal."))
            | None ->
                match findParam parameters "zone" with
                | Some zone ->
                    match UtcOffsetZoneLiteral.tryParse zone with
                    | Result.Ok parsed -> parsed.TotalMinutes
                    | Result.Error message -> raise (DashSpecParseException($"transformer '{transformerId}': {message}"))
                | None ->
                    raise (
                        DashSpecParseException(
                            $"transformer '{transformerId}': timeshift_to_iana requires zone = UTC±… or offset_minutes."))

        match TimeShiftToIanaResolver.tryResolve minutes ZoneResolveReference.Utc with
        | Result.Ok iana -> [| "offset_minutes", string minutes; "iana", iana; "lossy", "true" |]
        | Result.Error message ->
            raise (DashSpecParseException($"transformer '{transformerId}': {message}"))

    let validateToZone (transformerId: string) (parameters: (string * string)[]) =
        let zone = findParam parameters "zone"
        let offsetMinutes =
            findParam parameters "offset_minutes"
            |> Option.bind (fun raw ->
                match Int32.TryParse raw with
                | true, v -> Some v
                | _ ->
                    raise (
                        DashSpecParseException(
                            $"transformer '{transformerId}': offset_minutes requires an integer literal.")))

        match zone, offsetMinutes with
        | None, None ->
            raise (
                DashSpecParseException(
                    $"transformer '{transformerId}': to_zone requires TimeShift zone = UTC±… or offset_minutes (use iana_to_timeshift for IANA)."))
        | Some _, Some _ ->
            raise (
                DashSpecParseException(
                    $"transformer '{transformerId}': use either zone or offset_minutes for to_zone, not both."))
        | Some z, None ->
            if z.Contains('/') then
                raise (
                    DashSpecParseException(
                        $"transformer '{transformerId}': to_zone does not accept IANA; use transform use iana_to_timeshift."))

            match UtcOffsetZoneLiteral.tryParse z with
            | Result.Ok parsed ->
                [| "zone", z; "offset_minutes", string parsed.TotalMinutes; "zone_kind", "timeshift" |]
            | Result.Error message ->
                raise (DashSpecParseException($"transformer '{transformerId}': {message}"))
        | None, Some minutes ->
            match UtcOffsetZoneLiteral.tryParseOffsetMinutes minutes with
            | Result.Ok parsed -> [| "offset_minutes", string parsed.TotalMinutes |]
            | Result.Error message ->
                raise (DashSpecParseException($"transformer '{transformerId}': {message}"))

    let normalizeStep (transformerId: string) (pluginId: string) (parameters: (string * string)[]) =
        if String.Equals(pluginId, "to_zone", StringComparison.OrdinalIgnoreCase) then
            validateToZone transformerId parameters
        elif String.Equals(pluginId, "iana_to_timeshift", StringComparison.OrdinalIgnoreCase) then
            validateIanaToTimeShift transformerId parameters
        elif String.Equals(pluginId, "timeshift_to_iana", StringComparison.OrdinalIgnoreCase) then
            validateTimeShiftToIana transformerId parameters
        else
            parameters
