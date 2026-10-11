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
      Files: IReadOnlyList<string>
      Exclude: IReadOnlyList<string>
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
        let files = ResizeArray<string>()
        let exclude = ResizeArray<string>()
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
                | "files" -> files.AddRange(parseStringList parts.[1])
                | "exclude" -> exclude.AddRange(parseStringList parts.[1])
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
          Files = files :> IReadOnlyList<_>
          Exclude = exclude :> IReadOnlyList<_>
          Index = index :> IReadOnlyList<_> }

module DashSpecProjectLocator =

    /// <summary>Project file = `*.dsproj` (canonical — ADR-0089 Phase 2 / ADR-0101); `dashspec.toml` remains a compat marker.</summary>
    let private findProjectFile (full: string) =
        let dsproj =
            Directory.EnumerateFiles(full, "*.dsproj")
            |> Seq.sort
            |> Seq.tryHead

        match dsproj with
        | Some path -> Some path
        | None ->
            let legacy = Path.Combine(full, "dashspec.toml")
            if File.Exists legacy then Some legacy else None

    let tryFindConfig (specDirectory: string option) =
        match specDirectory with
        | None | Some "" -> None
        | Some dir ->
            let full = Path.GetFullPath dir
            findProjectFile full |> Option.map DashSpecProjectConfig.load

    let legacyIncludesAllowed (parseOptions: DashSpecParseOptions) (specDirectory: string option) =
        match tryFindConfig specDirectory with
        | Some cfg when cfg.DisableLegacyIncludes -> false
        | _ -> parseOptions.AllowLegacyIncludes

/// <summary>Phase 2 project membership (ADR-0089): `files` positives minus `exclude` — the linkable universe, lex-sorted and deterministic.</summary>
module DashSpecProjectMembership =

    let private isGlob (entry: string) = entry.Contains('*') || entry.Contains('?')

    let private expandEntry (root: string) (entry: string) =
        let normalized = entry.Replace('\\', '/')

        if not (isGlob normalized) then
            [ Path.GetFullPath(Path.Combine(root, entry)) ]
        else
            let dirPart = Path.GetDirectoryName(normalized)
            let pattern = Path.GetFileName(normalized)

            let dir =
                Path.GetFullPath(Path.Combine(root, if String.IsNullOrEmpty dirPart then "." else dirPart))

            if Directory.Exists dir then
                Directory.EnumerateFiles(dir, pattern)
                |> Seq.map Path.GetFullPath
                |> Seq.sort
                |> Seq.toList
            else
                []

    let expand (cfg: DashSpecProjectConfig) =
        let positives =
            cfg.Files
            |> Seq.collect (expandEntry cfg.ProjectRoot)
            |> Seq.distinct
            |> Seq.sort

        let excluded =
            cfg.Exclude
            |> Seq.collect (expandEntry cfg.ProjectRoot)
            |> Set.ofSeq

        positives |> Seq.filter (fun p -> not (excluded.Contains p)) |> Seq.toList :> IReadOnlyList<string>

    /// <summary>True when membership is not declared (unconstrained) or the path is a declared member.</summary>
    let isDeclared (cfg: DashSpecProjectConfig) (path: string) =
        cfg.Files.Count = 0
        || (expand cfg |> Seq.contains (Path.GetFullPath path))
