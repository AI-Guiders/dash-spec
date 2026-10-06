namespace DashSpec.Modeling.Core

open System

/// <summary>Resolves IANA tz database ids to a fixed <see cref="UtcOffsetZoneLiteral.Parsed"/> at a reference instant.</summary>
module IanaZoneConverter =

    let private tryFindTimeZone (timeZoneId: string) =
        match TimeZoneInfo.TryFindSystemTimeZoneById timeZoneId with
        | true, tz -> Some tz
        | _ -> None

    let private resolveTimeZone (ianaId: string) =
        match tryFindTimeZone ianaId with
        | Some tz -> Result.Ok tz
        | None ->
            match TimeZoneInfo.TryConvertIanaIdToWindowsId ianaId with
            | true, windowsId ->
                match tryFindTimeZone windowsId with
                | Some tz -> Result.Ok tz
                | None -> Result.Error $"Unknown IANA time zone '{ianaId}'."
            | _ -> Result.Error $"Unknown IANA time zone '{ianaId}'."

    /// <summary>Offset in minutes east of UTC at <paramref name="referenceUtc"/> (DST-aware for that instant).</summary>
    let tryToTimeShift (ianaId: string) (referenceUtc: DateTime) : Result<UtcOffsetZoneLiteral.Parsed, string> =
        if String.IsNullOrWhiteSpace ianaId then
            Result.Error "IANA zone id is required."
        elif ianaId.Contains(' ') then
            Result.Error "IANA zone id cannot contain spaces."
        else
            match resolveTimeZone (ianaId.Trim()) with
            | Result.Ok tz ->
                let reference = DateTime.SpecifyKind(referenceUtc, DateTimeKind.Utc)
                let offset = tz.GetUtcOffset reference
                Result.Ok { UtcOffsetZoneLiteral.TotalMinutes = int offset.TotalMinutes }
            | Result.Error message -> Result.Error message
