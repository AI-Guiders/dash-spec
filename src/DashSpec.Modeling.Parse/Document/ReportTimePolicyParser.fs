namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic

[<RequireQualifiedAccess>]
module ReportTimePolicyParser =
    let defaultPolicy =
        { Basis = ReportTimeBasis.Calendar
          Apply = ReportTimeApply.Clip
          WorkTimeColumn = None
          WorkCalendar = None }

    let private parseBasis (raw: string) =
        match raw.Trim().ToLowerInvariant() with
        | "working" | "work" -> ReportTimeBasis.Working
        | _ -> ReportTimeBasis.Calendar

    let private parseApply (raw: string) =
        match raw.Trim().ToLowerInvariant() with
        | "measure" -> ReportTimeApply.Measure
        | "both" -> ReportTimeApply.Both
        | _ -> ReportTimeApply.Clip

    let private configurationKeys =
        Set.ofList [ "time_basis"; "time_apply"; "work_time_column"; "work_timezone"; "work_start"; "work_end"; "work_days" ]

    let hasConfigurationKey (key: string) = configurationKeys.Contains key

    let mergeConfiguration (existing: ReportTimePolicy option) (props: IReadOnlyDictionary<string, string>) =
        if props.Count = 0 then existing
        elif props.Keys |> Seq.forall (fun k -> not (hasConfigurationKey k)) then existing
        else
            let basePolicy = existing |> Option.defaultValue defaultPolicy

            let basis =
                match props.TryGetValue "time_basis" with
                | true, raw -> parseBasis raw
                | false, _ -> basePolicy.Basis

            let apply =
                match props.TryGetValue "time_apply" with
                | true, raw -> parseApply raw
                | false, _ -> basePolicy.Apply

            let workColumn =
                match props.TryGetValue "work_time_column" with
                | true, raw when not (String.IsNullOrWhiteSpace raw) -> Some(raw.Trim())
                | _ -> basePolicy.WorkTimeColumn

            let priorCalendar = basePolicy.WorkCalendar |> Option.defaultValue { TimeZoneId = None; StartTime = None; EndTime = None; WorkDays = None }

            let calendar =
                if
                    props.ContainsKey "work_timezone"
                    || props.ContainsKey "work_start"
                    || props.ContainsKey "work_end"
                    || props.ContainsKey "work_days"
                then
                    Some
                        { TimeZoneId =
                            match props.TryGetValue "work_timezone" with
                            | true, raw when not (String.IsNullOrWhiteSpace raw) -> Some(raw.Trim())
                            | _ -> priorCalendar.TimeZoneId
                          StartTime =
                            match props.TryGetValue "work_start" with
                            | true, raw when not (String.IsNullOrWhiteSpace raw) -> Some(raw.Trim())
                            | _ -> priorCalendar.StartTime
                          EndTime =
                            match props.TryGetValue "work_end" with
                            | true, raw when not (String.IsNullOrWhiteSpace raw) -> Some(raw.Trim())
                            | _ -> priorCalendar.EndTime
                          WorkDays =
                            match props.TryGetValue "work_days" with
                            | true, raw when not (String.IsNullOrWhiteSpace raw) -> Some(raw.Trim())
                            | _ -> priorCalendar.WorkDays }
                else
                    basePolicy.WorkCalendar

            Some
                { Basis = basis
                  Apply = apply
                  WorkTimeColumn = workColumn
                  WorkCalendar = calendar }

    let mergePolicyOptions (basePolicy: ReportTimePolicy option) (overlay: ReportTimePolicy option) =
        match overlay with
        | None -> basePolicy
        | Some o ->
            match basePolicy with
            | None -> Some o
            | Some b ->
                let bc =
                    b.WorkCalendar
                    |> Option.defaultValue { TimeZoneId = None; StartTime = None; EndTime = None; WorkDays = None }

                let oc =
                    o.WorkCalendar
                    |> Option.defaultValue { TimeZoneId = None; StartTime = None; EndTime = None; WorkDays = None }

                Some
                    { Basis = o.Basis
                      Apply = o.Apply
                      WorkTimeColumn = o.WorkTimeColumn |> Option.orElse b.WorkTimeColumn
                      WorkCalendar =
                        Some
                            { TimeZoneId = oc.TimeZoneId |> Option.orElse bc.TimeZoneId
                              StartTime = oc.StartTime |> Option.orElse bc.StartTime
                              EndTime = oc.EndTime |> Option.orElse bc.EndTime
                              WorkDays = oc.WorkDays |> Option.orElse bc.WorkDays } }
