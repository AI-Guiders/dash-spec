namespace DashSpec.Modeling.Core

open System

module BuiltinScalarTransforms =

    /// <summary>
    /// Parse-time parameter contract, declared by each transform plugin next to its
    /// validate/normalize code: which literal kinds a parameter accepts and the hint
    /// that rides the diagnostic on mismatch. Layering: Core speaks in neutral literal
    /// terms; the parser maps them to its token kinds. No schema for a key → the
    /// generic free-form assignment reader.
    /// </summary>
    type ParamLiteral =
        | TimeShiftZone
        | IanaZoneName
        | AnyString

    type ParamSpec = {
        Key: string
        Literals: ParamLiteral list
        Hint: string
    }

    type ParamContract = {
        RequiresParameterBlock: bool
        Params: ParamSpec list
    }

    let private contract (requiresBlock: bool) (params': ParamSpec list) =
        { RequiresParameterBlock = requiresBlock; Params = params' }

    // Each plugin declares its own contract right where it is implemented.
    let private toZoneContract =
        contract true [
            { Key = "zone"
              Literals = [ TimeShiftZone; AnyString ]
              Hint = "to_zone requires a TimeShift literal (UTC±…); for IANA zones use transform use iana_to_timeshift." }
        ]

    let private ianaToTimeShiftContract =
        contract true [
            { Key = "iana"; Literals = [ IanaZoneName; AnyString ]; Hint = "iana_to_timeshift requires an IANA zone literal (e.g. Europe/Moscow)." }
            { Key = "zone"; Literals = [ IanaZoneName; AnyString ]; Hint = "iana_to_timeshift requires an IANA zone literal (e.g. Europe/Moscow)." }
        ]

    let private timeShiftToIanaContract =
        // Historical asymmetry, preserved: a bare step parses; validate* demands
        // zone/offset_minutes only when a parameter block exists.
        contract false [
            { Key = "zone"; Literals = [ TimeShiftZone; AnyString ]; Hint = "timeshift_to_iana requires a TimeShift literal (UTC±…)." }
        ]

    /// <summary>
    /// Execution locality of a transform step — the layering invariant (ADR-0126):
    /// database steps compile into the query (head of the chain, source frame),
    /// runtime steps work over the loaded batch (tail; to_zone/sort are display
    /// concerns). A Database step may never follow a Runtime one. The old dual-face
    /// order_by died: query order is <c>order</c> (database), display sort is
    /// <c>sort</c> (runtime) — the BI three-layer split.
    /// </summary>
    type TransformLocality =
        | Database
        | Runtime

    /// The closed vocabulary of transform plugin ids. An unknown id does not compile —
    /// a typo or a dead keyword must fail at parse time, not at render. New transforms
    /// register here together with their contract and locality.
    let knownPluginIds =
        [
            "summarize"
            "group"
            "order"
            "sort"
            "to_zone"
            "iana_to_timeshift"
            "timeshift_to_iana"
            "reduce"
        ]

    let isKnownPluginId (pluginId: string) =
        knownPluginIds
        |> List.exists (fun id -> String.Equals(id, pluginId, StringComparison.OrdinalIgnoreCase))

    /// Locality by plugin id; unknown ids never reach here (closed vocabulary at parse).
    let localityOf (pluginId: string) =
        if String.Equals(pluginId, "summarize", StringComparison.OrdinalIgnoreCase)
           || String.Equals(pluginId, "order", StringComparison.OrdinalIgnoreCase) then
            Database
        elif String.Equals(pluginId, "sort", StringComparison.OrdinalIgnoreCase)
             || String.Equals(pluginId, "group", StringComparison.OrdinalIgnoreCase)
             || String.Equals(pluginId, "to_zone", StringComparison.OrdinalIgnoreCase)
             || String.Equals(pluginId, "iana_to_timeshift", StringComparison.OrdinalIgnoreCase)
             || String.Equals(pluginId, "timeshift_to_iana", StringComparison.OrdinalIgnoreCase)
             || String.Equals(pluginId, "reduce", StringComparison.OrdinalIgnoreCase) then
            Runtime
        else
            // Unknown ids never reach here: the parse-time vocabulary check rejects them.
            Database

    /// The plugin's declared parameter contract, if any (case-insensitive id).
    let paramContract (pluginId: string) : ParamContract option =
        if String.Equals(pluginId, "to_zone", StringComparison.OrdinalIgnoreCase) then
            Some toZoneContract
        elif String.Equals(pluginId, "iana_to_timeshift", StringComparison.OrdinalIgnoreCase) then
            Some ianaToTimeShiftContract
        elif String.Equals(pluginId, "timeshift_to_iana", StringComparison.OrdinalIgnoreCase) then
            Some timeShiftToIanaContract
        else
            None

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
