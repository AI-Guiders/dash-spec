namespace DashSpec.Modeling.Core

open System
open System.Globalization

/// <summary>Validated RGB hex color value type (`#rgb` or `#rrggbb`, normalized to `#rrggbb`).</summary>
[<Struct>]
type HexColorValue =
    { Raw: string
      Normalized: string
      R: byte
      G: byte
      B: byte }

    override this.ToString() = this.Normalized

module HexColorValue =

    let tryParse (text: string) : Result<HexColorValue, string> =
        if String.IsNullOrWhiteSpace text then
            Result.Error "Hex color cannot be empty."
        else
            let t = text.Trim()
            if not (t.StartsWith('#')) then
                Result.Error $"Hex color '{t}' must start with '#'."
            else
                let hex = t.Substring(1)
                if hex.Length = 3 && hex |> Seq.forall Uri.IsHexDigit then
                    let expanded = String([| hex.[0]; hex.[0]; hex.[1]; hex.[1]; hex.[2]; hex.[2] |]).ToLowerInvariant()
                    let r = Byte.Parse(expanded.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                    let g = Byte.Parse(expanded.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                    let b = Byte.Parse(expanded.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                    Result.Ok { Raw = t; Normalized = "#" + expanded; R = r; G = g; B = b }
                elif hex.Length = 6 && hex |> Seq.forall Uri.IsHexDigit then
                    let lower = hex.ToLowerInvariant()
                    let r = Byte.Parse(lower.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                    let g = Byte.Parse(lower.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                    let b = Byte.Parse(lower.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                    Result.Ok { Raw = t; Normalized = "#" + lower; R = r; G = g; B = b }
                else
                    Result.Error $"Hex color '{t}' must be #rgb or #rrggbb."

/// <summary>Validated fixed UTC offset value type (`UTC`, `UTC+3`, `UTC-05:30`, ±14:00).</summary>
[<Struct>]
type TimeShiftValue =
    { Raw: string
      TotalMinutes: int }

    override this.ToString() = this.Raw

module TimeShiftValue =

    let tryParse (text: string) : Result<TimeShiftValue, string> =
        match UtcOffsetZoneLiteral.tryParse text with
        | Result.Ok parsed -> Result.Ok { Raw = text.Trim(); TotalMinutes = parsed.TotalMinutes }
        | Result.Error err -> Result.Error err

/// <summary>Validated IANA time zone identifier value type (`Area/Location`, e.g. `Europe/Moscow`).</summary>
[<Struct>]
type IanaZoneValue =
    { Id: string }

    override this.ToString() = this.Id

module IanaZoneValue =

    let tryParse (text: string) : Result<IanaZoneValue, string> =
        if String.IsNullOrWhiteSpace text then
            Result.Error "IANA zone id is required."
        else
            let trimmed = text.Trim()
            if not (trimmed.Contains('/')) || trimmed.Contains(' ') then
                Result.Error $"'{trimmed}' is not a valid IANA time zone id."
            else
                match IanaZoneConverter.tryToTimeShift trimmed DateTime.UtcNow with
                | Result.Ok _ -> Result.Ok { Id = trimmed }
                | Result.Error err -> Result.Error err

/// <summary>Validated relative day offset value type (`-Nd`, e.g. `-7d` → `Days = 7`, `SignedDays = -7`).</summary>
[<Struct>]
type RelativeDayValue =
    { Raw: string
      Days: int }

    member this.SignedDays = -this.Days
    override this.ToString() = this.Raw

module RelativeDayValue =

    let tryParse (text: string) : Result<RelativeDayValue, string> =
        if String.IsNullOrWhiteSpace text then
            Result.Error "Relative day cannot be empty."
        else
            let t = text.Trim()
            if t.Length >= 3 && t.[0] = '-' && (t.[t.Length - 1] = 'd' || t.[t.Length - 1] = 'D') then
                let digits = t.Substring(1, t.Length - 2)
                match Int32.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture) with
                | true, days when days >= 0 -> Result.Ok { Raw = t; Days = days }
                | _ -> Result.Error $"Invalid relative day '{t}'. Expected -Nd (e.g. -7d)."
            else
                Result.Error $"Invalid relative day '{t}'. Expected -Nd (e.g. -7d)."

/// <summary>Validated date bound value type (`today`, `-Nd`, or `yyyy-MM-dd`).</summary>
[<RequireQualifiedAccess>]
type DateBoundValue =
    | Today
    | Relative of RelativeDayValue
    | Calendar of DateOnly

    override this.ToString() =
        match this with
        | DateBoundValue.Today -> "today"
        | DateBoundValue.Relative r -> r.Raw
        | DateBoundValue.Calendar d -> d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)

module DateBoundValue =

    let tryParse (text: string) : Result<DateBoundValue, string> =
        if String.IsNullOrWhiteSpace text then
            Result.Error "Date bound cannot be empty."
        else
            let t = text.Trim()
            if String.Equals(t, "today", StringComparison.OrdinalIgnoreCase) then
                Result.Ok DateBoundValue.Today
            else
                match RelativeDayValue.tryParse t with
                | Result.Ok rel -> Result.Ok(DateBoundValue.Relative rel)
                | Result.Error _ ->
                    match DateOnly.TryParseExact(t, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None) with
                    | true, d -> Result.Ok(DateBoundValue.Calendar d)
                    | false, _ -> Result.Error $"Unknown date bound '{t}'. Use today, -Nd (e.g. -7d), or yyyy-MM-dd."

/// <summary>Validated date range value type (`from..to` where both bounds are <see cref="DateBoundValue"/>).</summary>
[<Struct>]
type DateRangeValue =
    { From: DateBoundValue
      To: DateBoundValue }

    override this.ToString() = $"{this.From}..{this.To}"

module DateRangeValue =

    let tryParseBounds (fromText: string) (toText: string) : Result<DateRangeValue, string> =
        match DateBoundValue.tryParse fromText, DateBoundValue.tryParse toText with
        | Result.Ok f, Result.Ok t -> Result.Ok { From = f; To = t }
        | Result.Error e, _ -> Result.Error e
        | _, Result.Error e -> Result.Error e
