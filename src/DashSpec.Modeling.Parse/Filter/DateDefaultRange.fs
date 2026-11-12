namespace DashSpec.Modeling.Parse.Filter

open System
open System.Text.RegularExpressions
open DashSpec.Modeling.Core

/// Parses date filter defaults from .dashspec (e.g. -7d..today, 2026-06-01..2026-06-30).
module DateDefaultRange =

    type private BoundKind =
        | Today
        | RelativeDay
        | Absolute

    let private relativeDayRegex =
        Regex(@"^-(?<days>\d+)d$", RegexOptions.IgnoreCase)

    let private splitRange (expression: string) =
        if String.IsNullOrWhiteSpace expression then
            raise (DashSpecParseException("Date filter requires explicit default range, e.g. default -7d..today"))

        let parts = expression.Split("..", StringSplitOptions.TrimEntries ||| StringSplitOptions.RemoveEmptyEntries)

        if parts.Length <> 2 then
            raise (DashSpecParseException($"Date default must be 'from..to' (e.g. -7d..today or 2026-06-01..2026-06-30), got '{expression.Trim()}'."))

        parts.[0], parts.[1]

    let private resolveBoundShape (token: string) =
        if token.Equals("today", StringComparison.OrdinalIgnoreCase) then Today
        elif relativeDayRegex.IsMatch token then RelativeDay
        elif
            match DateOnly.TryParse token with
            | true, _ -> true
            | false, _ -> false
        then
            Absolute
        else
            raise (DashSpecParseException($"Unknown date bound '{token}'. Use today, -Nd (e.g. -7d), or yyyy-MM-dd."))

    let tryParse (expression: string) : Result<DateRangeValue, string> =
        if String.IsNullOrWhiteSpace expression then
            Result.Error "Date filter requires explicit default range, e.g. default -7d..today"
        else
            let parts = expression.Split("..", StringSplitOptions.TrimEntries ||| StringSplitOptions.RemoveEmptyEntries)
            if parts.Length <> 2 then
                Result.Error $"Date default must be 'from..to' (e.g. -7d..today or 2026-06-01..2026-06-30), got '{expression.Trim()}'."
            else
                DateRangeValue.tryParseBounds parts.[0] parts.[1]

    let validateSyntax (expression: string) =
        match tryParse expression with
        | Result.Ok _ -> ()
        | Result.Error err -> raise (DashSpecParseException err)

    let validateSingleDayDefault (expression: string) =
        match tryParse expression with
        | Result.Ok range when range.From = range.To -> ()
        | Result.Ok _ ->
            raise (DashSpecParseException($"Date filter with widget=day requires a single-day default (from..to must match), e.g. today..today, got '{expression.Trim()}'."))
        | Result.Error err -> raise (DashSpecParseException err)
