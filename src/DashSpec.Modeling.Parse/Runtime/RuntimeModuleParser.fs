namespace DashSpec.Modeling.Parse.Runtime

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Syntax

module RuntimeModuleParser =

    let private unquote (raw: string) =
        let t = raw.Trim()
        if t.Length >= 2 && ((t.[0] = '"' && t.[t.Length - 1] = '"') || (t.[0] = '\'' && t.[t.Length - 1] = '\'')) then
            t.Substring(1, t.Length - 2)
        else
            t

    let private tryParseBool (raw: string option) : bool option =
        match raw with
        | Some s when not (String.IsNullOrWhiteSpace s) ->
            match (unquote s).Trim().ToLowerInvariant() with
            | "true" | "yes" | "1" -> Some true
            | "false" | "no" | "0" -> Some false
            | _ -> None
        | _ -> None

    let private tryParseInt (raw: string option) : int option =
        match raw with
        | Some s when not (String.IsNullOrWhiteSpace s) ->
            match Int32.TryParse((unquote s).Trim()) with
            | true, v -> Some v
            | _ -> None
        | _ -> None

    let private escapeAdoValue (value: string) =
        if value.IndexOfAny([| ';'; '='; '"'; '\''; ' ' |]) >= 0 then
            "\"" + value.Replace("\"", "\"\"") + "\""
        else
            value

    let private buildConnectionString
        (connector: string)
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
                let isPg =
                    String.Equals(connector, "postgres", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(connector, "postgresql", StringComparison.OrdinalIgnoreCase)

                if isPg then
                    server |> Option.iter (fun s -> parts.Add($"Host={escapeAdoValue s}"))
                    port |> Option.iter (fun p -> parts.Add($"Port={p}"))
                    database |> Option.iter (fun d -> parts.Add($"Database={escapeAdoValue d}"))
                    user |> Option.iter (fun u -> parts.Add($"Username={escapeAdoValue u}"))
                    password |> Option.iter (fun pw -> parts.Add($"Password={escapeAdoValue pw}"))
                else
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

            let connector =
                Construct.tryCell m "connector"
                |> Option.orElse (Construct.tryCell m "driver")
                |> Option.map unquote
                |> Option.defaultValue id

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
                buildConnectionString connector server port database user password integratedSecurity encrypt trustCert explicitConn

            { Id = id
              Connector = connector
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

    let parseEnvelope (env: UnitGrammar.UnitEnvelope) : RuntimeDocument =
        let providers = ResizeArray<RuntimeProviderDefinition>()
        let plugins = ResizeArray<string>()
        let mutable defaultProviderId = ""
        let mutable activeBundle = "standard"

        let decl = providerDeclaration providers.Add

        match Construct.tryFromAstTable env.Body with
        | Some tableForm -> Construct.apply decl tableForm
        | None -> ()

        let topProperties = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

        for m in env.Body do
            match m with
            | DashSpecAstNode.PropertyAssignment p ->
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
                | _ ->
                    topProperties.[k] <- p.ValueText
            | DashSpecAstNode.BlockDeclaration b ->
                match b.Opener with
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Provider, pid) ->
                    Construct.apply decl (Construct.fromAstBlock [ "id", pid ] b)
                | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Provider ->
                    Construct.apply decl (Construct.fromAstBlock [] b)
                | _ -> ()
            | _ -> ()

        if providers.Count = 0 && topProperties.Count > 0 then
            let pairs = topProperties |> Seq.map (fun kv -> KeyValuePair(kv.Key, kv.Value))
            Construct.apply decl (Construct.ConstructForm.BlockForm([], pairs))

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
        if String.IsNullOrWhiteSpace text then
            invalidArg "text" "Runtime module text is required."
        let tree = DashSpecSurfaceSyntax.parse text
        let env = UnitGrammar.unwrap DashSpecModuleKind.Runtime tree.Root
        parseEnvelope env

    let parseFile (path: string) : RuntimeDocument =
        if not (System.IO.File.Exists path) then
            raise (System.IO.FileNotFoundException("Runtime file not found.", path))
        parse (System.IO.File.ReadAllText path)

    do
        UnitProjector.register
            { KindKeyword = "runtime"
              ImportKind = Some ImportKind.Runtimes
              Role = UnitCompileRole.Declaration
              Project = fun ctx -> box (parseEnvelope ctx.Envelope) }
