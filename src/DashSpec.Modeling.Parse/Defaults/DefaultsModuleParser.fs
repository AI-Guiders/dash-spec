namespace DashSpec.Modeling.Parse.Defaults

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Syntax

[<CLIMutable>]
type DefaultsDocument =
    { Id: string
      Namespace: string option
      UsePreset: string option
      SqlDialect: string option
      Provider: string option
      TimeZone: string option
      Culture: string option
      TimeFormat: string option
      DateFormat: string option
      DateTimeFormat: string option
      FilterDefaults: IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>
      Properties: IReadOnlyDictionary<string, string> }

module DefaultsModuleParser =

    let private unquote (raw: string) =
        let t = raw.Trim()
        if t.Length >= 2 && ((t.[0] = '"' && t.[t.Length - 1] = '"') || (t.[0] = '\'' && t.[t.Length - 1] = '\'')) then
            t.Substring(1, t.Length - 2)
        else
            t

    let parseEnvelope (env: UnitGrammar.UnitEnvelope) : DefaultsDocument =
        let mutable usePreset: string option = None
        let mutable sqlDialect: string option = None
        let mutable provider: string option = None
        let mutable timeZone: string option = None
        let mutable culture: string option = None
        let mutable timeFormat: string option = None
        let mutable dateFormat: string option = None
        let mutable dateTimeFormat: string option = None
        let filterDefaults = Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        let properties = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

        let setFilterDefault (filterName: string) (propName: string) (value: string) =
            let bucket =
                match filterDefaults.TryGetValue filterName with
                | true, b -> b
                | false, _ ->
                    let b = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    filterDefaults.[filterName] <- b
                    b
            bucket.[propName] <- value

        let applyProperty (p: PropertySyntax) =
            let key = p.Key.Trim()
            let valText = (unquote p.ValueText).Trim()
            properties.[key] <- valText

            match p.KeyPath.Segments with
            | [| k |] ->
                match k.ToLowerInvariant() with
                | "use" -> usePreset <- Some valText
                | "sqldialect" | "sql_dialect" -> sqlDialect <- Some valText
                | "provider" -> provider <- Some valText
                | "time_zone" | "timezone" -> timeZone <- Some valText
                | "culture" -> culture <- Some valText
                | "time_format" -> timeFormat <- Some valText
                | "date_format" -> dateFormat <- Some valText
                | "datetime_format" -> dateTimeFormat <- Some valText
                | _ -> ()
            | [| prefix; filterName; propName |] when String.Equals(prefix, "filter", StringComparison.OrdinalIgnoreCase) ->
                if String.Equals(propName, "default", StringComparison.OrdinalIgnoreCase) then
                    raise (DashSpecParseException("In defaults module use filter.<id>.<property> (not '.default')."))
                setFilterDefault filterName propName valText
            | _ -> ()

        for m in env.Body do
            match m with
            | DashSpecAstNode.PropertyAssignment p ->
                applyProperty p
            | DashSpecAstNode.ClauseDeclaration c ->
                match c.Kind with
                | ClauseKind.UseClause(_, target) -> usePreset <- Some target
                | ClauseKind.UseProviderClause p ->
                    provider <-
                        match p with
                        | ProviderBindingSyntax.Infer -> Some "infer"
                        | ProviderBindingSyntax.Named id -> Some id
                | _ -> ()
            | DashSpecAstNode.BlockDeclaration b ->
                for child in b.Members do
                    match child with
                    | DashSpecAstNode.PropertyAssignment p -> applyProperty p
                    | _ -> ()
            | _ -> ()

        let roFilterDefaults =
            let d = Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            for kv in filterDefaults do
                d.[kv.Key] <- (kv.Value :> IReadOnlyDictionary<string, string>)
            d :> IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>

        { Id = env.Id
          Namespace = env.Namespace
          UsePreset = usePreset
          SqlDialect = sqlDialect
          Provider = provider
          TimeZone = timeZone
          Culture = culture
          TimeFormat = timeFormat
          DateFormat = dateFormat
          DateTimeFormat = dateTimeFormat
          FilterDefaults = roFilterDefaults
          Properties = properties :> IReadOnlyDictionary<string, string> }

    let parse (text: string) : DefaultsDocument =
        if String.IsNullOrWhiteSpace text then
            invalidArg "text" "Defaults module text is required."
        let tree = DashSpecSurfaceSyntax.parse text
        let env = UnitGrammar.unwrap DashSpecModuleKind.Defaults tree.Root
        parseEnvelope env

    let parseFile (path: string) : DefaultsDocument =
        if not (System.IO.File.Exists path) then
            raise (System.IO.FileNotFoundException("Defaults file not found.", path))
        parse (System.IO.File.ReadAllText path)

    do
        UnitProjector.register
            { KindKeyword = "defaults"
              ImportKind = Some ImportKind.Defaults
              Role = UnitCompileRole.Declaration
              Project = fun ctx -> box (parseEnvelope ctx.Envelope) }
