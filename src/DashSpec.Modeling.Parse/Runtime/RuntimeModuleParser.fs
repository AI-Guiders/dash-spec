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
