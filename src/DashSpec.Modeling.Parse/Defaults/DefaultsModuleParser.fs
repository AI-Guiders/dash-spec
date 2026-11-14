namespace DashSpec.Modeling.Parse.Defaults

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Document
open DashSpec.Modeling.Parse.Syntax

[<RequireQualifiedAccess>]
type FilterDefaultProperty =
    | Range
    | Limit
    | Scale
    | Min
    | Max

    member this.WireName =
        match this with
        | FilterDefaultProperty.Range -> "range"
        | FilterDefaultProperty.Limit -> "limit"
        | FilterDefaultProperty.Scale -> "scale"
        | FilterDefaultProperty.Min -> "min"
        | FilterDefaultProperty.Max -> "max"

module FilterDefaultProperty =

    let tryParse (raw: string) : FilterDefaultProperty option =
        match raw.Trim().ToLowerInvariant() with
        | "range" -> Some FilterDefaultProperty.Range
        | "limit" -> Some FilterDefaultProperty.Limit
        | "scale" -> Some FilterDefaultProperty.Scale
        | "min" -> Some FilterDefaultProperty.Min
        | "max" -> Some FilterDefaultProperty.Max
        | _ -> None

type FilterDefaultBinding =
    { FilterId: string
      Property: FilterDefaultProperty
      Value: PropertyValueSyntax }

[<CLIMutable>]
type DefaultsDocument =
    { Id: string
      Namespace: string option
      UsePreset: string option
      SqlDialect: SqlDialect option
      Provider: ProviderBindingSyntax option
      TimeZone: string option
      Formats: ReportFormatDefaults
      TimePolicy: ReportTimePolicy option
      FilterDefaults: FilterDefaultBinding[] }

module DefaultsModuleParser =

    let private unquote = Construct.unquote

    let private parseProviderBinding (raw: string) : ProviderBindingSyntax =
        let clean = (unquote raw).Trim()
        if String.Equals(clean, "infer", StringComparison.OrdinalIgnoreCase) then
            ProviderBindingSyntax.Infer
        else
            ProviderBindingSyntax.Named clean

    let parseEnvelope (env: UnitGrammar.UnitEnvelope) : DefaultsDocument =
        let mutable usePreset: string option = None
        let mutable sqlDialect: SqlDialect option = None
        let mutable provider: ProviderBindingSyntax option = None
        let mutable timeZone: string option = None
        let mutable formats = ReportFormatDefaults.empty
        let mutable timeBasis: ReportTimeBasis option = None
        let mutable timeApply: ReportTimeApply option = None
        let mutable workTimeColumn: string option = None
        let mutable workTimeZone: string option = None
        let mutable workStart: string option = None
        let mutable workEnd: string option = None
        let mutable workDays: string option = None
        let filterDefaults = ResizeArray<FilterDefaultBinding>()

        let addFilterDefault (filterName: string) (propName: string) (value: PropertyValueSyntax) =
            if String.Equals(propName, "default", StringComparison.OrdinalIgnoreCase) then
                raise (DashSpecParseException("In defaults module use filter.<id>.<property> (not '.default')."))
            match FilterDefaultProperty.tryParse propName with
            | Some prop ->
                filterDefaults.Add
                    { FilterId = filterName.Trim()
                      Property = prop
                      Value = value }
            | None ->
                raise (DashSpecParseException($"Unknown filter default property '{propName}'. Expected: range, limit, scale, min, max."))

        let applyProperty (p: PropertySyntax) =
            let valText = (unquote p.Value.Text).Trim()

            match p.KeyPath.Segments with
            | [| k |] ->
                match k.ToLowerInvariant() with
                | "use" -> usePreset <- Some valText
                | "sqldialect" | "sql_dialect" -> sqlDialect <- Some(SqlDialectParser.parse valText)
                | "provider" -> provider <- Some(parseProviderBinding valText)
                | "time_zone" | "timezone" -> timeZone <- Some valText
                | "culture" -> formats <- { formats with Culture = Some valText }
                | "time_format" -> formats <- { formats with TimeFormat = Some valText }
                | "date_format" -> formats <- { formats with DateFormat = Some valText }
                | "datetime_format" -> formats <- { formats with DateTimeFormat = Some valText }
                | "time_basis" -> timeBasis <- Some(ReportTimePolicyParser.parseBasis valText)
                | "time_apply" -> timeApply <- Some(ReportTimePolicyParser.parseApply valText)
                | "work_time_column" -> workTimeColumn <- Some valText
                | "work_timezone" -> workTimeZone <- Some valText
                | "work_start" -> workStart <- Some valText
                | "work_end" -> workEnd <- Some valText
                | "work_days" -> workDays <- Some valText
                | _ -> ()
            | [| prefix; filterName; propName |] ->
                match LanguageObjectKind.tryParse prefix with
                | Some LanguageObjectKind.Filter -> addFilterDefault filterName propName p.Value
                | _ -> ()
            | [| filterName; propName |]
                when (FilterDefaultProperty.tryParse propName).IsSome ->
                addFilterDefault filterName propName p.Value
            | _ -> ()

        let filterDecl : Construct.Declaration<FilterDefaultBinding list> =
            { Name = "filter"
              Vocabulary = [ "id"; "range"; "limit"; "scale"; "min"; "max" ]
              CellsToRow = fun m ->
                  match Construct.tryCell m "id" with
                  | Some fid ->
                      [ for prop in
                            [ FilterDefaultProperty.Range
                              FilterDefaultProperty.Limit
                              FilterDefaultProperty.Scale
                              FilterDefaultProperty.Min
                              FilterDefaultProperty.Max ] do
                          match Construct.tryCell m prop.WireName with
                          | Some v ->
                              yield
                                  { FilterId = unquote fid
                                    Property = prop
                                    Value = PropertyValueSyntax.Raw(unquote v) }
                          | None -> () ]
                  | None -> []
              ConstructRow = fun items ->
                  for item in items do
                      filterDefaults.Add item }

        match Construct.tryFromAstTable env.Body with
        | Some tableForm -> Construct.apply filterDecl tableForm
        | None -> ()

        for m in env.Body do
            match m with
            | DashSpecAstNode.PropertyAssignment p ->
                applyProperty p
            | DashSpecAstNode.ClauseDeclaration c ->
                match c.Kind with
                | ClauseKind.UseClause(_, target) -> usePreset <- Some target
                | ClauseKind.UseProviderClause p -> provider <- Some p
                | _ -> ()
            | DashSpecAstNode.BlockDeclaration b ->
                for child in b.Members do
                    match child with
                    | DashSpecAstNode.PropertyAssignment p -> applyProperty p
                    | _ -> ()
            | _ -> ()

        let timePolicy =
            if
                timeBasis.IsNone
                && timeApply.IsNone
                && workTimeColumn.IsNone
                && workTimeZone.IsNone
                && workStart.IsNone
                && workEnd.IsNone
                && workDays.IsNone
            then
                None
            else
                let cal =
                    if workTimeZone.IsSome || workStart.IsSome || workEnd.IsSome || workDays.IsSome then
                        Some
                            { TimeZoneId = workTimeZone
                              StartTime = workStart
                              EndTime = workEnd
                              WorkDays = workDays }
                    else
                        None
                Some
                    { Basis = timeBasis |> Option.defaultValue ReportTimeBasis.Calendar
                      Apply = timeApply |> Option.defaultValue ReportTimeApply.Clip
                      WorkTimeColumn = workTimeColumn
                      WorkCalendar = cal }

        { Id = env.Id
          Namespace = env.Namespace
          UsePreset = usePreset
          SqlDialect = sqlDialect
          Provider = provider
          TimeZone = timeZone
          Formats = formats
          TimePolicy = timePolicy
          FilterDefaults = filterDefaults.ToArray() }

    let parse (text: string) : DefaultsDocument =
        UnitGrammar.parseUnit DashSpecModuleKind.Defaults parseEnvelope text

    let parseFile (path: string) : DefaultsDocument =
        UnitGrammar.parseUnitFile DashSpecModuleKind.Defaults parseEnvelope path
