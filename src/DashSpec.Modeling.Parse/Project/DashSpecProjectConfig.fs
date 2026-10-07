namespace DashSpec.Modeling.Parse.Project

open System
open System.Collections.Generic
open System.IO
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse

/// <summary>One row in <c>dashspec.toml</c> <c>[[index]]</c> — maps logical namespace + kind to a glob under the project root.</summary>
type DashSpecProjectIndexEntry =
    { Namespace: string
      Kind: ImportKind
      Glob: string }

/// <summary>Project marker (<c>dashspec.toml</c>) shared by LSP and compile (ADR-0098).</summary>
type DashSpecProjectConfig =
    { ProjectRoot: string
      RootNamespace: string option
      SourceRoots: IReadOnlyList<string>
      DisableLegacyIncludes: bool
      Index: IReadOnlyList<DashSpecProjectIndexEntry> }

module DashSpecProjectConfig =

    let private trimQuotes (value: string) =
        let v = value.Trim()
        if v.Length >= 2 && v.[0] = '"' && v.[v.Length - 1] = '"' then v.Substring(1, v.Length - 2)
        else v

    let private parseBool (value: string) =
        match value.Trim().ToLowerInvariant() with
        | "true" | "yes" | "1" -> true
        | "false" | "no" | "0" -> false
        | _ -> raise (DashSpecParseException($"Invalid boolean in dashspec.toml: '{value}'."))

    let private parseStringList (value: string) =
        let inner =
            value.Trim()
            |> fun v ->
                if v.StartsWith("[") && v.EndsWith("]") then v.Substring(1, v.Length - 2)
                else v

        inner.Split(',', StringSplitOptions.RemoveEmptyEntries)
        |> Array.map (fun part -> trimQuotes (part.Trim()))
        |> Array.toList

    /// <summary>Minimal TOML reader for <c>root_namespace</c>, <c>source_roots</c>, <c>disable_legacy_includes</c>, and <c>[[index]]</c> tables.</summary>
    let load (path: string) =
        if String.IsNullOrWhiteSpace path then invalidArg "path" "Path is required."
        if not (File.Exists path) then raise (FileNotFoundException($"dashspec.toml not found: '{path}'.", path))

        let projectRoot = Path.GetDirectoryName path |> Option.ofObj |> Option.defaultValue ""

        let mutable rootNamespace: string option = None
        let mutable sourceRoots = [ "." ]
        let mutable disableLegacy = false
        let index = ResizeArray<DashSpecProjectIndexEntry>()
        let mutable current: DashSpecProjectIndexEntry option = None

        let flushIndex () =
            match current with
            | None -> ()
            | Some entry ->
                if String.IsNullOrWhiteSpace entry.Namespace then
                    raise (DashSpecParseException("dashspec.toml [[index]] requires namespace."))
                elif String.IsNullOrWhiteSpace entry.Glob then
                    raise (DashSpecParseException($"dashspec.toml index for '{entry.Namespace}' requires glob."))
                else
                    index.Add entry
                    current <- None

        for line in File.ReadAllLines path do
            let trimmed = line.Trim()

            if trimmed.StartsWith("#") || trimmed.Length = 0 then ()
            elif trimmed = "[[index]]" then
                flushIndex ()
                current <- Some { Namespace = ""; Kind = ImportKind.Diagrams; Glob = "" }
            elif trimmed.Contains('=') then
                let parts = trimmed.Split('=', 2)
                let key = parts.[0].Trim().ToLowerInvariant()
                let value = trimQuotes (parts.[1].Trim())

                match key with
                | "root_namespace" -> rootNamespace <- Some value
                | "source_roots" -> sourceRoots <- parseStringList parts.[1]
                | "disable_legacy_includes" -> disableLegacy <- parseBool value
                | "namespace" ->
                    match current with
                    | None -> ()
                    | Some entry -> current <- Some { entry with Namespace = value }
                | "kind" ->
                    match current, ImportKindRegistry.tryFindByKeyword value with
                    | None, _ -> ()
                    | Some _, None -> raise (DashSpecParseException($"Unknown import kind '{value}' in dashspec.toml."))
                    | Some entry, Some kind -> current <- Some { entry with Kind = kind }
                | "glob" ->
                    match current with
                    | None -> ()
                    | Some entry -> current <- Some { entry with Glob = value }
                | _ -> ()

        flushIndex ()

        { ProjectRoot = projectRoot
          RootNamespace = rootNamespace
          SourceRoots = sourceRoots :> IReadOnlyList<_>
          DisableLegacyIncludes = disableLegacy
          Index = index :> IReadOnlyList<_> }

module DashSpecProjectLocator =

    let tryFindConfig (specDirectory: string option) =
        match specDirectory with
        | None | Some "" -> None
        | Some dir ->
            let full = Path.GetFullPath dir
            let candidate = Path.Combine(full, "dashspec.toml")

            if File.Exists candidate then
                Some(DashSpecProjectConfig.load candidate)
            else
                None

    let legacyIncludesAllowed (parseOptions: DashSpecParseOptions) (specDirectory: string option) =
        match tryFindConfig specDirectory with
        | Some cfg when cfg.DisableLegacyIncludes -> false
        | _ -> parseOptions.AllowLegacyIncludes
