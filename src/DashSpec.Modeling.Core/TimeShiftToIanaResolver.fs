namespace DashSpec.Modeling.Core

open System

module TimeShiftToIanaResolver =

    let private pickBetter (current: string option) (candidate: string) =
        match current with
        | None -> Some candidate
        | Some existing ->
            if String.Compare(candidate, existing, StringComparison.OrdinalIgnoreCase) < 0 then
                Some candidate
            else
                Some existing

    let tryResolve (offsetMinutes: int) (referenceUtc: DateTime) : Result<string, string> =
        let reference = DateTime.SpecifyKind(referenceUtc, DateTimeKind.Utc)
        let mutable best: string option = None

        for tz in TimeZoneInfo.GetSystemTimeZones() do
            let minutes = int (tz.GetUtcOffset reference).TotalMinutes

            if minutes = offsetMinutes then
                let candidate =
                    match TimeZoneInfo.TryConvertWindowsIdToIanaId tz.Id with
                    | true, iana -> iana
                    | _ -> tz.Id

                best <- pickBetter best candidate

        match best with
        | None -> Result.Error $"No IANA zone found for offset {offsetMinutes} minutes at the reference instant."
        | Some iana -> Result.Ok iana
