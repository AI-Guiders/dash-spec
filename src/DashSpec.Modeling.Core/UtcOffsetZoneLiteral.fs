namespace DashSpec.Modeling.Core

open System
open System.Text.RegularExpressions

/// <summary>Fixed UTC offset literals for dashflow <c>to_zone</c> (ADR-0078/0080). IANA ids are not accepted.</summary>
module UtcOffsetZoneLiteral =

    let private ZonePattern =
        Regex(
            @"^UTC(?<sign>[+-])?(?:(?<hours>\d{1,2})(?::(?<minutes>\d{2}))?)?$",
            RegexOptions.IgnoreCase ||| RegexOptions.CultureInvariant)

    type Parsed = { TotalMinutes: int }

    let tryParse (text: string) : Result<Parsed, string> =
        if String.IsNullOrWhiteSpace text then
            Result.Error "zone value is required."
        else
            let trimmed = text.Trim()

            if trimmed.Contains('/') || trimmed.Contains('\\') then
                Result.Error "zone must be a fixed UTC offset (e.g. UTC+3), not an IANA or Windows time zone id."
            elif trimmed.Contains(' ') then
                Result.Error "zone must be a fixed UTC offset (e.g. UTC+3), not a named time zone."
            else
                let m = ZonePattern.Match trimmed

                if not m.Success then
                    Result.Error "zone must match UTC, UTC+3, UTC+03:30, or use offset_minutes = <integer>."
                else
                    let sign =
                        match m.Groups.["sign"].Value with
                        | "-" -> -1
                        | _ -> 1

                    let hoursGroup = m.Groups.["hours"]

                    if not hoursGroup.Success then
                        Result.Ok { TotalMinutes = 0 }
                    else
                        let hours = Int32.Parse hoursGroup.Value

                        let minutes =
                            if m.Groups.["minutes"].Success then
                                Int32.Parse m.Groups.["minutes"].Value
                            else
                                0

                        if hours > 14 || minutes > 59 then
                            Result.Error "UTC offset hours must be 0–14 and minutes 0–59."
                        else
                            Result.Ok { TotalMinutes = sign * (hours * 60 + minutes) }

    let tryParseOffsetMinutes (minutes: int) : Result<Parsed, string> =
        if minutes < -14 * 60 || minutes > 14 * 60 then
            Result.Error "offset_minutes must be between -840 and 840 (±14:00)."
        else
            Result.Ok { TotalMinutes = minutes }
