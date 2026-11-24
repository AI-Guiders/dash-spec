namespace DashSpec.Modeling.Parse

open System
open System.Collections.Concurrent
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Syntax

[<RequireQualifiedAccess>]
type UnitCompileRole =
    | Declaration
    | Consumer
    | Root

type UnitProjectionContext =
    { Text: string
      Envelope: UnitGrammar.UnitEnvelope
      SpecDirectory: string option }

type UnitPluginDescriptor =
    { KindKeyword: string
      ImportKind: ImportKind option
      Role: UnitCompileRole
      Project: UnitProjectionContext -> obj }

type RegistryDocument =
    { Id: string
      Namespace: string option
      Properties: System.Collections.Generic.IReadOnlyDictionary<string, string>
      Columns: string[]
      Rows: System.Collections.Generic.IReadOnlyList<Map<string, string>> }

/// <summary>First-class <c>module registry &lt;id&gt;</c> unit parser (ADR-0131):
/// projects declarative tabular registries (e.g. <c>stdlib/units/units.ds</c>, <c>stdlib/rules/rules.ds</c>,
/// or user-authored registries) directly from the AST.</summary>
module RegistryModuleParser =

    let parseEnvelope (env: UnitGrammar.UnitEnvelope) : RegistryDocument =
        let props = System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        for p in env.View.Properties do
            props.[p.Key] <- p.Value.Text

        let columns =
            env.View.TableRows
            |> Array.tryPick (fun r ->
                match r.Kind with
                | TableRowKind.Header cols -> Some cols
                | _ -> None)
            |> Option.defaultValue [||]

        let rows = ResizeArray<Map<string, string>>()
        match Construct.tryFromAstTable env.Body with
        | Some(Construct.ConstructForm.TableForm tableRows) when tableRows.Length > 1 ->
            let header = tableRows.[0] |> Array.map (fun c -> c.Trim())
            for r in tableRows.[1..] do
                let isSep = r.Length > 0 && r |> Array.forall (fun c -> c.Length > 0 && c |> Seq.forall (fun ch -> ch = '-' || ch = ':'))
                if not isSep then
                    let m =
                        [ for i in 0 .. (min (r.Length - 1) (header.Length - 1)) do
                            let k = header.[i]
                            let v = r.[i].Trim()
                            if k.Length > 0 && v.Length > 0 && v <> "-" then
                                yield k, v ]
                        |> Map.ofList
                    rows.Add m
        | _ -> ()

        { Id = env.Id
          Namespace = env.Namespace
          Properties = props
          Columns = columns
          Rows = rows :> System.Collections.Generic.IReadOnlyList<_> }

    let parse (text: string) : RegistryDocument =
        if String.IsNullOrWhiteSpace text then
            invalidArg "text" "Registry text is required."
        let tree = DashSpecSurfaceSyntax.parse text
        let env = UnitGrammar.unwrap DashSpecModuleKind.Registry tree.Root
        parseEnvelope env

/// <summary>Pure microkernel unit plugin registry: sits directly above <see cref="UnitGrammar"/>
/// and knows zero concrete unit domains. Each vertical domain (built-in or external plugin)
/// registers its own <see cref="UnitPluginDescriptor"/> here.</summary>
module UnitProjector =

    let private plugins =
        ConcurrentDictionary<string, UnitPluginDescriptor>(StringComparer.OrdinalIgnoreCase)

    let register (descriptor: UnitPluginDescriptor) =
        if not (String.IsNullOrWhiteSpace descriptor.KindKeyword) then
            plugins.[descriptor.KindKeyword.Trim()] <- descriptor

    let registerCustomProjector (kind: string) (projector: UnitGrammar.UnitEnvelope -> obj) =
        register
            { KindKeyword = kind
              ImportKind = None
              Role = UnitCompileRole.Consumer
              Project = fun ctx -> projector ctx.Envelope }

    let tryResolve (kindKeyword: string) : UnitPluginDescriptor option =
        if String.IsNullOrWhiteSpace kindKeyword then
            None
        else
            match plugins.TryGetValue(kindKeyword.Trim()) with
            | true, d -> Some d
            | false, _ -> None

    let registeredKinds () : string list =
        plugins.Keys |> Seq.sort |> Seq.toList

    let project (text: string) (specDirectory: string option) : string * string * obj =
        if String.IsNullOrWhiteSpace text then
            invalidArg "text" "Unit text is required."

        let tree = DashSpecSurfaceSyntax.parse text

        match UnitGrammar.tryUnwrapAny tree.Root with
        | None ->
            raise (DashSpecUnitHeaderException("Unit requires 'module <kind> <id>'."))
        | Some env ->
            let kw = DashSpecModuleKind.toKeyword env.Kind
            match tryResolve kw with
            | Some descriptor ->
                let payload =
                    descriptor.Project
                        { Text = text
                          Envelope = env
                          SpecDirectory = specDirectory }
                kw, env.Id, payload
            | None ->
                raise (DashSpecUnitHeaderException($"Unknown module kind '{kw}'.", env.ModuleNode.Span.Start))

    do
        register
            { KindKeyword = "registry"
              ImportKind = Some ImportKind.Registries
              Role = UnitCompileRole.Declaration
              Project = fun ctx -> box (RegistryModuleParser.parseEnvelope ctx.Envelope) }

namespace DashSpec.Modeling.Parse.Tooltip

open System
open System.Collections.Generic
open System.Text.RegularExpressions
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Syntax

[<CLIMutable>]
type TooltipDefinition =
    { Id: string
      Variables: IReadOnlyDictionary<string, string>
      Template: string }

module TooltipModuleParser =

    let private slotRegex = Regex(@"\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled)

    let private validatePlaceholders (id: string) (variables: IReadOnlyDictionary<string, string>) (template: string) =
        for m in slotRegex.Matches(template) do
            let slot = m.Groups.[1].Value
            if not (variables.ContainsKey slot) then
                raise (DashSpecParseException($"Tooltip '{id}': placeholder '{{{slot}}}' is not declared in variables."))

    /// Project a tooltip definition from any container's AST members (shared by `module tooltip <id>` and inline `tooltip <id>`).
    let projectFromMembers (id: string) (members: DashSpecAstNode[]) : TooltipDefinition =
        let view = UnitGrammar.view members
        let mutable variables: IReadOnlyDictionary<string, string> option = None
        let mutable template: string option = None
        let mutable source: string option = None

        for node in view.Body do
            match node with
            | DashSpecAstNode.BlockDeclaration b ->
                match b.Opener with
                | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Variables ->
                    if variables.IsSome then
                        raise (DashSpecParseException($"Tooltip '{id}': duplicate variables block."))

                    let vars = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    let innerView = UnitGrammar.view b.Members

                    if innerView.Body.Length <> innerView.Properties.Length then
                        raise (DashSpecParseException($"Tooltip '{id}' variables expects 'key = column' assignments."))

                    for prop in innerView.Properties do
                        if prop.ValueTokens.Length <> 1 || prop.ValueTokens.[0].LexKind <> TokenKind.Ident then
                            raise (DashSpecParseException($"Tooltip '{id}' variables: '{prop.Key}' requires an identifier column."))
                        vars.[prop.Key] <- prop.ValueTokens.[0].Text

                    if vars.Count = 0 then
                        raise (DashSpecParseException($"Tooltip '{id}' variables requires at least one entry."))

                    variables <- Some(vars :> IReadOnlyDictionary<string, string>)
                | _ ->
                    let headerText = b.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                    raise (DashSpecParseException($"Unexpected block '{headerText}' in tooltip '{id}'."))
            | DashSpecAstNode.PropertyAssignment p ->
                if String.Equals(p.Key, "tooltip", StringComparison.OrdinalIgnoreCase) then
                    if p.ValueTokens.Length <> 1 || p.ValueTokens.[0].LexKind <> TokenKind.String then
                        raise (DashSpecParseException($"Tooltip '{id}': tooltip requires a string literal."))
                    template <- Some(Construct.unquote p.ValueTokens.[0].Text)
                elif String.Equals(p.Key, "source", StringComparison.OrdinalIgnoreCase) then
                    if p.ValueTokens.Length <> 1 || p.ValueTokens.[0].LexKind <> TokenKind.Ident then
                        raise (DashSpecParseException($"Tooltip '{id}': source requires a column name."))
                    let col = p.ValueTokens.[0].Text
                    if String.IsNullOrWhiteSpace col then
                        raise (DashSpecParseException($"Tooltip '{id}': source requires a column name."))
                    source <- Some col
                else
                    raise (DashSpecParseException($"Tooltip '{id}': unexpected property '{p.Key}'."))
            | other ->
                let text = DashSpecAst.tokens other |> Array.map (fun t -> t.Text) |> String.concat " "
                raise (DashSpecParseException($"Unexpected statement '{text}' in tooltip '{id}'."))

        let resolvedVariables, resolvedTemplate =
            match source with
            | Some col when variables.IsSome || template.IsSome ->
                raise (DashSpecParseException($"Tooltip '{id}': source cannot be combined with variables/tooltip."))
            | Some col ->
                let vars = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                vars.["value"] <- col
                vars :> IReadOnlyDictionary<string, string>, "{value}"
            | None ->
                match variables, template with
                | Some v, Some t -> v, t
                | _ ->
                    if variables.IsNone || variables.Value.Count = 0 then
                        raise (DashSpecParseException($"Tooltip '{id}': variables (or source) is required."))
                    if template.IsNone || String.IsNullOrWhiteSpace template.Value then
                        raise (DashSpecParseException($"Tooltip '{id}': tooltip = \"...\" is required."))
                    variables.Value, template.Value

        validatePlaceholders id resolvedVariables resolvedTemplate
        { Id = id; Variables = resolvedVariables; Template = resolvedTemplate }

    let parseEnvelope (env: UnitGrammar.UnitEnvelope) : TooltipDefinition =
        projectFromMembers env.Id env.Body

    let parseTooltipFile (text: string) : TooltipDefinition =
        UnitGrammar.parseUnit DashSpecModuleKind.Tooltip parseEnvelope text

    let parseTooltipFileWithId (text: string) =
        let def = parseTooltipFile text
        def.Id, def

    /// <summary>Parse inline tooltip body embedded in @card / @diagram (ADR-0048).</summary>
    let parseTooltipInlineBody (id: string) (bodyText: string) : TooltipDefinition =
        if String.IsNullOrWhiteSpace id then invalidArg "id" "Tooltip id is required."
        let tree = DashSpecSurfaceSyntax.parse bodyText
        projectFromMembers id (DashSpecAst.members tree.Root)

    /// <summary>Parse inline tooltip from current reader position (@diagram / @card) via AST projection.</summary>
    let parseInline (reader: TokenReader) (id: string) : TooltipDefinition =
        parseTooltipInlineBody id (TokenReaderTooltip.readBodySource reader)

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
                | ClauseKind.UsePresetClause target
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

namespace DashSpec.Modeling.Parse.Texts

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Syntax

type TextBinding =
    { Target: LanguageObjectRef
      Label: string }

[<CLIMutable>]
type TextsDocument =
    { Id: string
      Namespace: string option
      Language: string option
      Bindings: TextBinding[] }

module TextsModuleParser =

    let private unquote = Construct.unquote

    let private isLabelProperty (prop: string) =
        String.Equals(prop, "label", StringComparison.OrdinalIgnoreCase)
        || String.Equals(prop, "title", StringComparison.OrdinalIgnoreCase)
        || String.Equals(prop, "text", StringComparison.OrdinalIgnoreCase)
        || String.Equals(prop, "value", StringComparison.OrdinalIgnoreCase)

    let private entryDeclaration
        (addBinding: TextBinding -> unit)
        : Construct.Declaration<TextBinding>
        =
        let cellsToBinding (m: Map<string, string>) : TextBinding =
            let rawTarget =
                Construct.tryCell m "target"
                |> Option.orElse (Construct.tryCell m "id")
                |> Option.orElse (Construct.tryCell m "key")
                |> Option.map unquote
                |> Option.defaultWith (fun () ->
                    raise (DashSpecParseException("texts table row requires a 'target' (or 'id') cell.")))

            let label =
                Construct.tryCell m "label"
                |> Option.orElse (Construct.tryCell m "title")
                |> Option.orElse (Construct.tryCell m "text")
                |> Option.orElse (Construct.tryCell m "value")
                |> Option.map unquote
                |> Option.defaultValue ""

            { Target = LanguageObjectRef.parse rawTarget
              Label = label }

        { Name = "texts"
          Vocabulary = [ "target"; "id"; "key"; "label"; "title"; "text"; "value" ]
          CellsToRow = cellsToBinding
          ConstructRow = addBinding }

    let parseEnvelope (env: UnitGrammar.UnitEnvelope) : TextsDocument =
        let bindings = ResizeArray<TextBinding>()
        let mutable language: string option = None

        let decl = entryDeclaration bindings.Add

        Construct.projectAst decl None "target" env.Body

        for p in env.View.Properties do
            let k = p.Key.Trim()
            let v = unquote p.ValueText
            if String.Equals(k, "language", StringComparison.OrdinalIgnoreCase) ||
               String.Equals(k, "culture", StringComparison.OrdinalIgnoreCase) then
                language <- Some v
            elif isLabelProperty p.KeyPath.MemberName && p.KeyPath.IsQualified then
                () // projected via Construct.projectAst DottedForm
            else
                bindings.Add
                    { Target = LanguageObjectRef.ofDotSyntax p.KeyPath
                      Label = v }

        { Id = env.Id
          Namespace = env.Namespace
          Language = language
          Bindings = bindings.ToArray() }

    let parse (text: string) : TextsDocument =
        UnitGrammar.parseUnit DashSpecModuleKind.Texts parseEnvelope text

    let parseFile (path: string) : TextsDocument =
        UnitGrammar.parseUnitFile DashSpecModuleKind.Texts parseEnvelope path

namespace DashSpec.Modeling.Parse.Runtime

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Syntax

[<RequireQualifiedAccess>]
type RuntimeConnectorKind =
    | SqlServer
    | Postgres
    | DuckDb
    | Custom of id: string

    member this.Name =
        match this with
        | RuntimeConnectorKind.SqlServer -> "sqlserver"
        | RuntimeConnectorKind.Postgres -> "postgres"
        | RuntimeConnectorKind.DuckDb -> "duckdb"
        | RuntimeConnectorKind.Custom id -> id

module RuntimeConnectorKind =

    let parse (raw: string) : RuntimeConnectorKind =
        match raw.Trim().ToLowerInvariant() with
        | "sqlserver" | "mssql" | "tsql" -> RuntimeConnectorKind.SqlServer
        | "postgres" | "postgresql" | "pg" -> RuntimeConnectorKind.Postgres
        | "duckdb" -> RuntimeConnectorKind.DuckDb
        | _ -> RuntimeConnectorKind.Custom(raw.Trim())

[<CLIMutable>]
type RuntimeProviderDefinition =
    { Id: string
      ConnectorKind: RuntimeConnectorKind
      Server: string option
      Port: int option
      Database: string option
      User: string option
      Password: string option
      IntegratedSecurity: bool option
      Encrypt: bool option
      TrustServerCertificate: bool option
      TimeoutSeconds: int
      MaxRows: int
      ConnectionString: string }

    member this.Connector = this.ConnectorKind.Name

[<CLIMutable>]
type RuntimeDocument =
    { Id: string
      Namespace: string option
      DefaultProviderId: string
      ActiveBundle: string
      Providers: IReadOnlyList<RuntimeProviderDefinition>
      Plugins: IReadOnlyList<string> }

module RuntimeModuleParser =

    let private unquote = Construct.unquote
    let private tryParseBool = Construct.tryParseBool
    let private tryParseInt = Construct.tryParseInt

    let private escapeAdoValue (value: string) =
        if value.IndexOfAny([| ';'; '='; '"'; '\''; ' ' |]) >= 0 then
            "\"" + value.Replace("\"", "\"\"") + "\""
        else
            value

    let private buildConnectionString
        (connectorKind: RuntimeConnectorKind)
        (server: string option)
        (port: int option)
        (database: string option)
        (user: string option)
        (password: string option)
        (integratedSecurity: bool option)
        (encrypt: bool option)
        (trustCert: bool option)
        (explicitConnStr: string option)
        : string
        =
        match explicitConnStr with
        | Some cs when not (String.IsNullOrWhiteSpace cs) -> cs
        | _ ->
            match server, database with
            | None, None -> ""
            | _ ->
                let parts = ResizeArray<string>()

                match connectorKind with
                | RuntimeConnectorKind.Postgres ->
                    server |> Option.iter (fun s -> parts.Add($"Host={escapeAdoValue s}"))
                    port |> Option.iter (fun p -> parts.Add($"Port={p}"))
                    database |> Option.iter (fun d -> parts.Add($"Database={escapeAdoValue d}"))
                    user |> Option.iter (fun u -> parts.Add($"Username={escapeAdoValue u}"))
                    password |> Option.iter (fun pw -> parts.Add($"Password={escapeAdoValue pw}"))
                | RuntimeConnectorKind.SqlServer
                | RuntimeConnectorKind.DuckDb
                | RuntimeConnectorKind.Custom _ ->
                    match server, port with
                    | Some s, Some p ->
                        let sp = $"{s},{p}"
                        parts.Add($"Server={escapeAdoValue sp}")
                    | Some s, None -> parts.Add($"Server={escapeAdoValue s}")
                    | None, _ -> ()

                    database |> Option.iter (fun d -> parts.Add($"Database={escapeAdoValue d}"))

                    match user with
                    | Some u when not (String.IsNullOrWhiteSpace u) ->
                        parts.Add($"User Id={escapeAdoValue u}")
                        password |> Option.iter (fun pw -> parts.Add($"Password={escapeAdoValue pw}"))
                    | _ ->
                        match integratedSecurity with
                        | Some true -> parts.Add("Trusted_Connection=True")
                        | Some false -> ()
                        | None ->
                            if password.IsNone then
                                parts.Add("Trusted_Connection=True")

                    match encrypt with
                    | Some b -> parts.Add(if b then "Encrypt=True" else "Encrypt=False")
                    | None -> ()

                    match trustCert with
                    | Some b -> parts.Add(if b then "TrustServerCertificate=True" else "TrustServerCertificate=False")
                    | None ->
                        parts.Add("TrustServerCertificate=True")

                String.Join(";", parts)

    let private providerDeclaration
        (addProvider: RuntimeProviderDefinition -> unit)
        : Construct.Declaration<RuntimeProviderDefinition>
        =
        let cellsToProvider (m: Map<string, string>) : RuntimeProviderDefinition =
            let id =
                Construct.tryCell m "id"
                |> Option.orElse (Construct.tryCell m "provider")
                |> Option.map unquote
                |> Option.defaultValue "sqlserver"

            let connectorRaw =
                Construct.tryCell m "connector"
                |> Option.orElse (Construct.tryCell m "driver")
                |> Option.map unquote
                |> Option.defaultValue id

            let connectorKind = RuntimeConnectorKind.parse connectorRaw
            let server = Construct.tryCell m "server" |> Option.orElse (Construct.tryCell m "host") |> Option.map unquote
            let port = Construct.tryCell m "port" |> tryParseInt
            let database = Construct.tryCell m "database" |> Option.orElse (Construct.tryCell m "db") |> Option.map unquote
            let user = Construct.tryCell m "user" |> Option.orElse (Construct.tryCell m "username") |> Option.map unquote
            let password = Construct.tryCell m "password" |> Option.map unquote
            let integratedSecurity =
                Construct.tryCell m "integrated_security"
                |> Option.orElse (Construct.tryCell m "trusted_connection")
                |> tryParseBool
            let encrypt = Construct.tryCell m "encrypt" |> tryParseBool
            let trustCert = Construct.tryCell m "trust_server_certificate" |> tryParseBool
            let timeout =
                Construct.tryCell m "timeout_seconds"
                |> Option.orElse (Construct.tryCell m "command_timeout_seconds")
                |> tryParseInt
                |> Option.defaultValue 0
            let maxRows =
                Construct.tryCell m "max_rows"
                |> tryParseInt
                |> Option.defaultValue 0
            let explicitConn =
                Construct.tryCell m "connection_string"
                |> Option.map unquote

            let connStr =
                buildConnectionString connectorKind server port database user password integratedSecurity encrypt trustCert explicitConn

            { Id = id
              ConnectorKind = connectorKind
              Server = server
              Port = port
              Database = database
              User = user
              Password = password
              IntegratedSecurity = integratedSecurity
              Encrypt = encrypt
              TrustServerCertificate = trustCert
              TimeoutSeconds = timeout
              MaxRows = maxRows
              ConnectionString = connStr }

        { Name = "provider"
          Vocabulary =
            [ "id"
              "provider"
              "connector"
              "driver"
              "server"
              "host"
              "port"
              "database"
              "db"
              "user"
              "username"
              "password"
              "integrated_security"
              "trusted_connection"
              "encrypt"
              "trust_server_certificate"
              "timeout_seconds"
              "command_timeout_seconds"
              "max_rows"
              "connection_string" ]
          CellsToRow = cellsToProvider
          ConstructRow = addProvider }

    let private isRuntimeHeaderKey (key: string) =
        match key.Trim().ToLowerInvariant() with
        | "default" | "default_provider" | "default_provider_id"
        | "bundle" | "active_bundle" | "plugins" -> true
        | _ -> false

    let parseEnvelope (env: UnitGrammar.UnitEnvelope) : RuntimeDocument =
        let providers = ResizeArray<RuntimeProviderDefinition>()
        let plugins = ResizeArray<string>()
        let mutable defaultProviderId = ""
        let mutable activeBundle = "standard"

        let decl = providerDeclaration providers.Add

        // Unified TableForm + BlockForm (`provider <id> ... end provider`) + DottedForm (`<id>.<prop> = ...`)
        Construct.projectAst decl (Some DashSpecBlockKeyword.Provider) "id" env.Body

        let flatProviderProps = ResizeArray<KeyValuePair<string, string>>()

        for p in env.View.Properties do
            let k = p.Key.Trim()
            let v = (unquote p.ValueText).Trim()
            match k.ToLowerInvariant() with
            | "default" | "default_provider" | "default_provider_id" ->
                defaultProviderId <- v
            | "bundle" | "active_bundle" ->
                activeBundle <- v
            | "plugins" ->
                match p.Value with
                | PropertyValueSyntax.BracketList items
                | PropertyValueSyntax.CommaList items ->
                    for item in items do
                        let s = (unquote item.Text).Trim()
                        if not (String.IsNullOrWhiteSpace s) then
                            plugins.Add s
                | _ ->
                    let inner = v.Trim().TrimStart('[').TrimEnd(']')
                    for part in inner.Split([| ','; ' ' |], StringSplitOptions.RemoveEmptyEntries) do
                        let s = (unquote part).Trim()
                        if not (String.IsNullOrWhiteSpace s) then
                            plugins.Add s
            | _ when not p.KeyPath.IsQualified && not (isRuntimeHeaderKey k) ->
                flatProviderProps.Add(KeyValuePair(k, p.ValueText))
            | _ -> ()

        if providers.Count = 0 && flatProviderProps.Count > 0 then
            Construct.apply decl (Construct.ConstructForm.BlockForm([], flatProviderProps))

        if String.IsNullOrWhiteSpace defaultProviderId then
            defaultProviderId <-
                if providers.Count > 0 then providers.[0].Id else "sqlserver"

        { Id = env.Id
          Namespace = env.Namespace
          DefaultProviderId = defaultProviderId
          ActiveBundle = activeBundle
          Providers = providers :> IReadOnlyList<_>
          Plugins = plugins :> IReadOnlyList<_> }

    let parse (text: string) : RuntimeDocument =
        UnitGrammar.parseUnit DashSpecModuleKind.Runtime parseEnvelope text

    let parseFile (path: string) : RuntimeDocument =
        UnitGrammar.parseUnitFile DashSpecModuleKind.Runtime parseEnvelope path

namespace DashSpec.Modeling.Parse.Host

open System
open System.Collections.Generic
open System.IO
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Layout
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Syntax

[<CLIMutable>]
type HostLinkDefinition =
    { Id: string
      Label: string
      Url: string
      Target: string
      Topbar: bool
      Settings: bool }

[<CLIMutable>]
type HostDocument =
    { Id: string
      CatalogPath: string
      Configuration: IReadOnlyDictionary<string, string>
      Presentation: IReadOnlyDictionary<string, string>
      Host: IReadOnlyDictionary<string, string>
      Links: IReadOnlyList<HostLinkDefinition>
      Surfaces: IReadOnlyList<string>
      ShellLayout: DashSpec.Modeling.Parse.Layout.LayoutBoardDefinition option }

module HostModuleParser =

    let private unquote = Construct.unquote

    let private importShellLayout (specDirectory: string option) (sourceOffset: int) (namespaceName: string) =
        match specDirectory with
        | None
        | Some "" ->
            raise (DashSpecParseException("import layouts requires host file directory when parsing.", sourceOffset))
        | Some dir ->
            let index = DashSpec.Modeling.Parse.Document.UnitIndex.build dir

            let entries =
                index.FindForKind(namespaceName, "layouts", "layout")

            match entries with
            | [ entry ] ->
                let board =
                    LayoutModuleParser.parseLayoutFile (
                        DashSpec.Modeling.Parse.Document.UnitSource.readUnit entry.Path
                    )

                LayoutModuleScopeValidator.ensureMatchesImportSite board LayoutScope.Host "import layouts"
                board
            | [] ->
                raise (DashSpecParseException($"No module found for import layouts from '{namespaceName}'.", sourceOffset))
            | _ ->
                raise (DashSpecParseException($"Host imports more than one layouts unit from '{namespaceName}'.", sourceOffset))

    let private parseBoolCell (id: string) (col: string) (defaultValue: bool) (raw: string option) =
        match raw with
        | None -> defaultValue
        | Some v when String.IsNullOrWhiteSpace v -> defaultValue
        | Some v ->
            match v.Trim().ToLowerInvariant() with
            | "true" -> true
            | "false" -> false
            | other ->
                raise (
                    DashSpecParseException(
                        $"links '{col}' for '{id}' must be true or false, got '{other}'."
                    )
                )

    let private linkDeclaration (addLink: HostLinkDefinition -> unit) : Construct.Declaration<HostLinkDefinition> =
        let cellsToLink (m: Map<string, string>) : HostLinkDefinition =
            match Construct.tryCell m "id" with
            | Some rawId when not (String.IsNullOrWhiteSpace rawId) ->
                let linkId = rawId.Trim()

                let url =
                    match Construct.tryCell m "url" with
                    | Some u when not (String.IsNullOrWhiteSpace u) -> unquote u
                    | _ -> raise (DashSpecParseException($"Host link '{linkId}' requires url."))

                let label =
                    match Construct.tryCell m "label" with
                    | Some l when not (String.IsNullOrWhiteSpace l) -> unquote l
                    | _ -> linkId

                let target =
                    match Construct.tryCell m "target" with
                    | Some t when not (String.IsNullOrWhiteSpace t) -> unquote t
                    | _ -> "_blank"

                { Id = linkId
                  Label = label
                  Url = url
                  Target = target
                  Topbar = parseBoolCell linkId "topbar" true (Construct.tryCell m "topbar")
                  Settings = parseBoolCell linkId "settings" true (Construct.tryCell m "settings") }
            | _ -> raise (DashSpecParseException("links row requires a non-empty 'id' cell."))

        { Name = "links"
          Vocabulary = [ "id"; "label"; "url"; "target"; "topbar"; "settings" ]
          CellsToRow = cellsToLink
          ConstructRow = addLink }

    let private projectLinksBlock (block: BlockDeclarationSyntax) (links: ResizeArray<HostLinkDefinition>) =
        let decl = linkDeclaration links.Add
        let view = UnitGrammar.view block.Members

        for child in view.Blocks do
            match child.Opener with
            | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Link, linkId) ->
                let label = DashSpecAst.tryTitle (DashSpecAstNode.BlockDeclaration child) |> Option.defaultValue linkId
                let _ =
                    PropertyBlockParser.projectPropertiesFromAst
                        child.Members
                        PropertySchemas.hostLink
                        $"host link '{linkId}'"
                        false
                let form = Construct.fromAstBlock [ "id", linkId; "label", label ] child
                Construct.apply decl form
            | _ ->
                let headerText = child.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                raise (DashSpecParseException($"Unexpected block '{headerText}' in host links."))

        match Construct.tryFromAstTable block.Members with
        | Some tableForm -> Construct.apply decl tableForm
        | None -> ()

    let private parseSurfaceItems (prop: PropertySyntax) (surfaces: ResizeArray<string>) =
        match prop.Value with
        | PropertyValueSyntax.BracketList items
        | PropertyValueSyntax.CommaList items ->
            for item in items do
                let trimmed = (unquote item.Text).Trim()
                if not (String.IsNullOrWhiteSpace trimmed) then
                    surfaces.Add trimmed
        | _ ->
            for part in prop.ValueText.Trim().TrimStart('[').TrimEnd(']').Split(',', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries) do
                let trimmed = (unquote part).Trim()
                if not (String.IsNullOrWhiteSpace trimmed) then
                    surfaces.Add trimmed

    let private projectSurfacesBlock (block: BlockDeclarationSyntax) (surfaces: ResizeArray<string>) =
        let view = UnitGrammar.view block.Members
        for node in view.Body do
            match node with
            | DashSpecAstNode.PropertyAssignment p when String.Equals(p.Key, "show", StringComparison.OrdinalIgnoreCase) ->
                parseSurfaceItems p surfaces
            | _ ->
                let tokens = DashSpecAst.tokens node |> Array.filter (fun t -> t.LexKind <> TokenKind.LineComment && t.LexKind <> TokenKind.DocComment && t.LexKind <> TokenKind.BlockComment)
                match Array.toList tokens with
                | { LexKind = TokenKind.Ident; Text = kw } :: rest when String.Equals(kw, "show", StringComparison.OrdinalIgnoreCase) ->
                    for tok in rest do
                        if tok.LexKind = TokenKind.Ident then
                            surfaces.Add tok.Text
                        elif tok.LexKind <> TokenKind.Comma then
                            raise (DashSpecParseException($"Unexpected token '{tok.Text}' in host surfaces."))
                | other ->
                    let text = other |> List.map (fun t -> t.Text) |> String.concat " "
                    raise (DashSpecParseException($"Unexpected statement '{text}' in host surfaces."))

    let private applyHostProperty
        (key: string)
        (rawVal: string)
        (propNode: PropertySyntax option)
        (catalogPath: string byref)
        (configuration: Dictionary<string, string>)
        (presentation: Dictionary<string, string>)
        (hostSettings: Dictionary<string, string>)
        (surfaces: ResizeArray<string>)
        =
        let k = key.Trim().ToLowerInvariant()
        let v = unquote rawVal
        match k with
        | "catalog" ->
            catalogPath <- v
        | "surfaces" ->
            match propNode with
            | Some p -> parseSurfaceItems p surfaces
            | None ->
                for part in v.TrimStart('[').TrimEnd(']').Split(',', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries) do
                    surfaces.Add(unquote part)
        | "language"
        | "display_timezone" ->
            configuration.[k] <- v
            hostSettings.[k] <- v
        | "product_title"
        | "catalog_label"
        | "color_scheme"
        | "large_field_filter_layout"
        | "render" ->
            presentation.[k] <- v
            hostSettings.[k] <- v
        | "provider_default" ->
            hostSettings.[k] <- v
        | "sections"
        | "collapse_duplicate" ->
            presentation.[$"menu_{k}"] <- v
            hostSettings.[k] <- v
        | _ ->
            raise (DashSpecParseException($"Unknown property '{key}' in host module."))

    let parseEnvelope (env: UnitGrammar.UnitEnvelope) (specDirectory: string option) : HostDocument =
        let hostId = env.Id

        let mutable catalogPath = ""
        let configuration = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        let presentation = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        let hostSettings = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        let links = ResizeArray<HostLinkDefinition>()
        let surfaces = ResizeArray<string>()
        let mutable shellLayout = None

        let importedCatalogNamespaces = ResizeArray<string>()

        for imp in env.Imports do
            let kind = imp.Kind |> Option.defaultValue ""
            if String.Equals(kind, "layouts", StringComparison.OrdinalIgnoreCase) then
                shellLayout <- Some(importShellLayout specDirectory imp.Span.Start imp.Target)
            elif String.Equals(kind, "catalogs", StringComparison.OrdinalIgnoreCase) then
                importedCatalogNamespaces.Add imp.Target
            else
                raise (DashSpecParseException($"@host module allows import layouts or import catalogs only, got import {kind}.", imp.Span.Start))

        for node in env.View.Body do
            match node with
            | DashSpecAstNode.PropertyAssignment p ->
                applyHostProperty p.Key p.ValueText (Some p) &catalogPath configuration presentation hostSettings surfaces
            | DashSpecAstNode.ClauseDeclaration c ->
                match c.Kind with
                | ClauseKind.DirectiveClause("catalog", args) when args.Length >= 1 ->
                    catalogPath <- unquote args.[0]
                | _ ->
                    let text = c.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                    raise (DashSpecParseException($"Unexpected clause '{text}' in host '{hostId}'."))
            | DashSpecAstNode.BlockDeclaration b ->
                match b.Opener with
                | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Configuration ->
                    for kv in PropertyBlockParser.projectPropertiesFromAst b.Members PropertySchemas.hostConfiguration "configuration" false do
                        configuration.[kv.Key] <- kv.Value
                | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Presentation ->
                    for kv in PropertyBlockParser.projectPropertiesFromAst b.Members PropertySchemas.hostPresentation "presentation" false do
                        presentation.[kv.Key] <- kv.Value
                | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Menu ->
                    for kv in PropertyBlockParser.projectPropertiesFromAst b.Members PropertySchemas.hostMenu "menu" false do
                        presentation.[$"menu_{kv.Key}"] <- kv.Value
                | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Host ->
                    for kv in PropertyBlockParser.projectPropertiesFromAst b.Members PropertySchemas.hostSettings "host" false do
                        applyHostProperty kv.Key kv.Value None &catalogPath configuration presentation hostSettings surfaces
                | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Links ->
                    projectLinksBlock b links
                | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Surfaces ->
                    projectSurfacesBlock b surfaces
                | _ ->
                    let headerText = b.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                    raise (DashSpecParseException($"Unexpected block '{headerText}' in host '{hostId}'."))
            | other ->
                let text = DashSpecAst.tokens other |> Array.map (fun t -> t.Text) |> String.concat " "
                raise (DashSpecParseException($"Unexpected statement '{text}' in host '{hostId}'."))

        // Resolve catalog by unit index (module id or imported catalogs namespace) when specDirectory exists.
        match specDirectory with
        | Some dir when not (String.IsNullOrWhiteSpace dir) && Directory.Exists dir ->
            let index = DashSpec.Modeling.Parse.Document.UnitIndex.build dir
            if not (String.IsNullOrWhiteSpace catalogPath) then
                match index.TryFindUnit("catalog", catalogPath) with
                | Some entry ->
                    catalogPath <- Path.GetRelativePath(dir, entry.Path).Replace('\\', '/')
                | None -> ()
            elif importedCatalogNamespaces.Count > 0 then
                let found =
                    importedCatalogNamespaces
                    |> Seq.tryPick (fun ns -> index.TryFindUnit("catalog", ns))
                match found with
                | Some entry ->
                    catalogPath <- Path.GetRelativePath(dir, entry.Path).Replace('\\', '/')
                | None -> ()
        | _ -> ()

        if String.IsNullOrWhiteSpace catalogPath then
            raise (DashSpecParseException($"@host '{hostId}' requires catalog = <id> (or import catalogs from <Namespace>)."))

        { Id = hostId
          CatalogPath = catalogPath
          Configuration = configuration
          Presentation = presentation
          Host = hostSettings :> IReadOnlyDictionary<string, string>
          Links = links :> IReadOnlyList<_>
          Surfaces = surfaces :> IReadOnlyList<_>
          ShellLayout = shellLayout }

    let parse (text: string) (specDirectory: string option) : HostDocument =
        UnitGrammar.parseUnit DashSpecModuleKind.Host (fun env -> parseEnvelope env specDirectory) text

    let parseFile (path: string) : HostDocument =
        if String.IsNullOrWhiteSpace path then invalidArg "path" "Host file path is required."
        let directory = Path.GetDirectoryName(Path.GetFullPath path)
        UnitGrammar.parseUnitFile DashSpecModuleKind.Host (fun env -> parseEnvelope env (Some directory)) path

namespace DashSpec.Modeling.Parse.Transform

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Syntax

[<CLIMutable>]
type SeriesTransformBlock =
    { UsePreset: string option
      Max: int option
      OtherLabel: string option }

module TransformModuleParser =

    let private toBlock (props: System.Collections.Generic.Dictionary<string, string>) =
        if props.Count = 0 then
            raise (DashSpecParseException("@transform module requires at least one property."))

        let usePreset =
            match props.TryGetValue "use" with
            | true, value -> Some value
            | false, _ -> None

        let max =
            match props.TryGetValue "max" with
            | true, raw ->
                match Int32.TryParse raw with
                | true, parsed when parsed > 0 -> Some parsed
                | _ -> None
            | false, _ -> None

        let other =
            match props.TryGetValue "other" with
            | true, value -> Some value
            | false, _ -> None

        { UsePreset = usePreset; Max = max; OtherLabel = other }

    let parseEnvelope (env: UnitGrammar.UnitEnvelope) : SeriesTransformBlock =
        for b in env.View.Blocks do
            if b.Opener <> DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Series then
                let headerText = b.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                raise (DashSpecParseException($"Expected 'transform series', got '{headerText}'."))

        let propMembers = UnitGrammar.unwrapOptionalBlock DashSpecBlockKeyword.Series env.Body
        let view = UnitGrammar.view propMembers

        // If the `module transform` unit is a data-flow transform declaration (`type_effect` / `locality` / `param`),
        // validate its `type_effect` and return an empty SeriesTransformBlock for legacy callers.
        let isFlowStepDeclaration =
            view.Properties
            |> Array.exists (fun p ->
                (p.KeyTokens.Length = 2 && String.Equals(p.KeyTokens.[0].Text, "param", StringComparison.OrdinalIgnoreCase))
                || String.Equals(p.Key, "type_effect", StringComparison.OrdinalIgnoreCase)
                || String.Equals(p.Key, "locality", StringComparison.OrdinalIgnoreCase)
                || String.Equals(p.Key, "requires_block", StringComparison.OrdinalIgnoreCase)
                || String.Equals(p.Key, "hint", StringComparison.OrdinalIgnoreCase))

        if isFlowStepDeclaration then
            for p in view.Properties do
                if String.Equals(p.Key, "type_effect", StringComparison.OrdinalIgnoreCase) then
                    let name = p.ValueText.Trim()
                    if TransformDeclarations.TransformTypeEffect.tryParse name |> Option.isNone then
                        raise (
                            DashSpecInvalidValueException(
                                $"transform '{env.Id}': unknown type_effect '{name}' — expected identity|projection|extend|compose|require_equal|opaque.",
                                "type_effect",
                                p.Span.Start))

            { UsePreset = None; Max = None; OtherLabel = None }
        else
            let props =
                PropertyBlockParser.projectPropertiesFromAst
                    propMembers
                    PropertyBlockParser.seriesTransformSchema
                    "transform series"
                    false

            toBlock props

    let parseTransformFile (text: string) : SeriesTransformBlock =
        UnitGrammar.parseUnit DashSpecModuleKind.Transform parseEnvelope text

namespace DashSpec.Modeling.Parse.Palette

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Syntax

[<CLIMutable>]
type PaletteDocument =
    { Id: string
      Properties: IReadOnlyDictionary<string, string> }

module PaletteModuleParser =

    let private unquote = Construct.unquote

    let rec private parseColorOperandTokens (tokens: SyntaxToken list) : string * SyntaxToken list =
        match tokens with
        | { LexKind = TokenKind.Ident; Text = fn } :: { LexKind = TokenKind.LParen } :: rest
            when String.Equals(fn, "color", StringComparison.OrdinalIgnoreCase) ->
            let inner, afterInner = parseColorOperandTokens rest
            match afterInner with
            | { LexKind = TokenKind.RParen } :: tail -> inner, tail
            | _ -> raise (DashSpecInvalidValueException("Expected ')' after color(...) operand."))
        | { LexKind = TokenKind.String; Text = s } :: tail ->
            unquote s, tail
        | { LexKind = TokenKind.HexColor; Text = h } :: tail ->
            h, tail
        | { LexKind = TokenKind.Ident; Text = id } :: tail ->
            id, tail
        | _ ->
            raise (DashSpecInvalidValueException("Expected color literal (#rrggbb), CSS name, const reference, or color(...)."))

    let private parseSingleColorOperand (tokens: SyntaxToken[]) (context: string) : string =
        let operand, rest = parseColorOperandTokens (Array.toList tokens)
        if not rest.IsEmpty then
            raise (DashSpecInvalidValueException($"Unexpected trailing tokens in {context}."))
        operand

    let private parseColorsPropertyTokens (tokens: SyntaxToken[]) (constants: IReadOnlyDictionary<string, string>) : string =
        match Array.toList tokens with
        | [ { LexKind = TokenKind.String; Text = s } ] -> unquote s
        | { LexKind = TokenKind.LBracket } :: rest ->
            let items = ResizeArray<string>()
            let mutable cur = rest
            let mutable closed = false

            while not cur.IsEmpty && not closed do
                match cur with
                | { LexKind = TokenKind.RBracket } :: tail ->
                    closed <- true
                    cur <- tail
                | _ ->
                    let operand, afterOperand = parseColorOperandTokens cur
                    items.Add operand
                    cur <-
                        match afterOperand with
                        | { LexKind = TokenKind.Comma } :: tail -> tail
                        | tail -> tail

            if not closed || not cur.IsEmpty then
                raise (DashSpecInvalidValueException("Unclosed or malformed colors list '[ ... ]'."))

            if items.Count = 0 then
                raise (DashSpecInvalidValueException("colors list requires at least one entry."))

            items
            |> Seq.map (fun operand -> PaletteColorResolver.resolveOperand operand constants "colors list")
            |> PaletteColorResolver.joinColorList
        | _ ->
            raise (DashSpecInvalidValueException("Expected colors list [ … ] or string."))

    /// Project a palette definition from any container's AST members (shared by `module palette <id>` and `palette` blocks).
    let projectFromMembers (id: string) (members: DashSpecAstNode[]) : PaletteDocument =
        let flatMembers = UnitGrammar.unwrapOptionalBlock DashSpecBlockKeyword.Palette members
        let view = UnitGrammar.view flatMembers

        if view.Body.Length <> view.Properties.Length then
            raise (DashSpecUnexpectedStatementException($"Palette '{id}' contains unexpected non-property statements."))

        let constants = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        let values = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

        for prop in view.Properties do
            match prop.KeyTokens with
            | [| { LexKind = TokenKind.Ident; Text = kw }; { LexKind = TokenKind.Ident; Text = constName } |]
                when String.Equals(kw, "const", StringComparison.OrdinalIgnoreCase) ->
                let operand = parseSingleColorOperand prop.ValueTokens $"const '{constName}'"
                let hex = PaletteColorResolver.resolveOperand operand constants $"const '{constName}'"
                constants.[constName] <- hex
            | [| { LexKind = TokenKind.Ident; Text = key } |] ->
                if String.Equals(key, "colors", StringComparison.OrdinalIgnoreCase) then
                    values.[key] <- parseColorsPropertyTokens prop.ValueTokens constants
                else
                    let operand = parseSingleColorOperand prop.ValueTokens $"palette entry '{key}'"
                    values.[key] <- PaletteColorResolver.resolveOperand operand constants $"palette entry '{key}'"
            | [| { LexKind = TokenKind.String; Text = rawKey } |] ->
                let key = unquote rawKey
                if String.IsNullOrWhiteSpace key then
                    raise (DashSpecInvalidValueException("Quoted property key cannot be empty."))
                let operand = parseSingleColorOperand prop.ValueTokens $"palette entry '{key}'"
                values.[key] <- PaletteColorResolver.resolveOperand operand constants $"palette entry '{key}'"
            | _ ->
                raise (DashSpecUnknownPropertyException(prop.Key, $"Invalid property key '{prop.Key}' in palette '{id}'."))

        if values.Count = 0 then
            raise (DashSpecMissingMemberException("colors", $"Palette '{id}' requires at least one mapping entry."))

        { Id = id; Properties = values :> IReadOnlyDictionary<_, _> }

    let parseEnvelope (env: UnitGrammar.UnitEnvelope) : PaletteDocument =
        projectFromMembers env.Id env.Body

    let parsePaletteFile (text: string) : PaletteDocument =
        UnitGrammar.parseUnit DashSpecModuleKind.Palette parseEnvelope text

    let parsePaletteFileWithId (text: string) =
        let doc = parsePaletteFile text
        doc.Id, doc

namespace DashSpec.Modeling.Parse.Presentation

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Syntax

[<CLIMutable>]
type PresentationBlock =
    { UsePreset: string option
      Properties: IReadOnlyDictionary<string, string> }

[<CLIMutable>]
type PresentationModuleDocument =
    { Id: string
      Local: PresentationBlock option }

module PresentationModuleParser =

    let private toBlock (props: IDictionary<string, string>) =
        let usePreset =
            match props.TryGetValue "use" with
            | true, value -> Some value
            | false, _ -> None
        let inlineProps =
            let map = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            for kv in props do
                if not (kv.Key.Equals("use", StringComparison.OrdinalIgnoreCase)) then
                    map.[kv.Key] <- kv.Value
            map :> IReadOnlyDictionary<string, string>
        { UsePreset = usePreset; Properties = inlineProps }

    let parseEnvelope (env: UnitGrammar.UnitEnvelope) : PresentationModuleDocument =
        let propMembers = UnitGrammar.unwrapOptionalBlock DashSpecBlockKeyword.Presentation env.Body

        let props =
            PropertyBlockParser.projectPropertiesFromAst
                propMembers
                (PropertySchemas.presentation ())
                "presentation"
                false

        if props.Count = 0 then
            raise (DashSpecParseException("@presentation module requires at least one property."))

        { Id = env.Id; Local = Some(toBlock props) }

    let parsePresentationModule (text: string) : PresentationModuleDocument =
        UnitGrammar.parseUnit DashSpecModuleKind.Presentation parseEnvelope text

    let parsePresentationModuleWithId (text: string) =
        let doc = parsePresentationModule text
        doc.Id, doc

