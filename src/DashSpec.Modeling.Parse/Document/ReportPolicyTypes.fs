namespace DashSpec.Modeling.Parse.Document

open System

type SqlDialect =
    | TSql
    | Postgres
    | Generic

[<CLIMutable>]
type ReportFormatDefaults =
    { TimeFormat: string option
      DateFormat: string option
      DateTimeFormat: string option
      Culture: string option }

    static member Empty =
        { TimeFormat = None
          DateFormat = None
          DateTimeFormat = None
          Culture = None }

[<RequireQualifiedAccess>]
module ReportFormatDefaults =
    let empty =
        { TimeFormat = None; DateFormat = None; DateTimeFormat = None; Culture = None }

    let merge (baseFmt: ReportFormatDefaults) (overlay: ReportFormatDefaults) =
        { TimeFormat = overlay.TimeFormat |> Option.orElse baseFmt.TimeFormat
          DateFormat = overlay.DateFormat |> Option.orElse baseFmt.DateFormat
          DateTimeFormat = overlay.DateTimeFormat |> Option.orElse baseFmt.DateTimeFormat
          Culture = overlay.Culture |> Option.orElse baseFmt.Culture }

[<RequireQualifiedAccess>]
type ReportTimeBasis =
    | Calendar
    | Working

[<RequireQualifiedAccess>]
type ReportTimeApply =
    | Clip
    | Measure
    | Both

[<CLIMutable>]
type WorkCalendarDefinition =
    { TimeZoneId: string option
      StartTime: string option
      EndTime: string option
      WorkDays: string option }

[<CLIMutable>]
type ReportTimePolicy =
    { Basis: ReportTimeBasis
      Apply: ReportTimeApply
      WorkTimeColumn: string option
      WorkCalendar: WorkCalendarDefinition option }

    static member Default =
        { Basis = ReportTimeBasis.Calendar
          Apply = ReportTimeApply.Clip
          WorkTimeColumn = None
          WorkCalendar = None }

    member this.ClipsQueries =
        this.Basis = ReportTimeBasis.Working
        && (this.Apply = ReportTimeApply.Clip || this.Apply = ReportTimeApply.Both)
        && (this.WorkTimeColumn |> Option.exists (String.IsNullOrWhiteSpace >> not))
