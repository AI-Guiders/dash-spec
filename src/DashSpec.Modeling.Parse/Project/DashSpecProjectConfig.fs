namespace DashSpec.Modeling.Parse.Project

open System
open System.Collections.Generic
open System.IO
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse

/// <summary>Project marker (<c>dashspec.toml</c>) shared by LSP and compile (ADR-0098).</summary>
type DashSpecProjectConfig =
    { ProjectRoot: string
      RootNamespace: string option
      SourceRoots: IReadOnlyList<string>
      Files: IReadOnlyList<string>
      Exclude: IReadOnlyList<string>
      /// <summary>Implicit imports (DSL block, raw strings like "presentations from DashSpec.Std"):
      /// every module in the project sees these namespaces without a per-module import.</summary>
      ImplicitImports: IReadOnlyList<string> }

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

    /// <summary>DSL reader for <c>module project</c>: namespace + global import + optional files/exclude.</summary>
    let load (path: string) =
        if String.IsNullOrWhiteSpace path then invalidArg "path" "Path is required."
        if not (File.Exists path) then raise (FileNotFoundException($"dashspec.toml not found: '{path}'.", path))

        let projectRoot = Path.GetDirectoryName path |> Option.ofObj |> Option.defaultValue ""

        let mutable rootNamespace: string option = None
        let mutable sourceRoots = [ "." ]
        let files = ResizeArray<string>()
        let exclude = ResizeArray<string>()
        let implicitImports = ResizeArray<string>()

        let inProjectBlock = ref false

        for line in File.ReadAllLines path do
            let trimmed = line.Trim()

            if trimmed.StartsWith("#") || trimmed.Length = 0 then ()
            elif trimmed.StartsWith("module project ", StringComparison.OrdinalIgnoreCase) then
                inProjectBlock := true
            elif trimmed.Equals("end project", StringComparison.OrdinalIgnoreCase) then
                inProjectBlock := false
            elif not !inProjectBlock && trimmed.Contains('=') then
                // Legacy TOML keys outside the block (backward compat)
                let parts = trimmed.Split('=', 2)
                let key = parts.[0].Trim().ToLowerInvariant()
                let value = trimQuotes (parts.[1].Trim())

                match key with
                | "root_namespace" -> rootNamespace <- Some value
                | "source_roots" -> sourceRoots <- parseStringList parts.[1]
                | "files" -> files.AddRange(parseStringList parts.[1])
                | "exclude" -> exclude.AddRange(parseStringList parts.[1])
                | "global_import" -> implicitImports.AddRange(parseStringList parts.[1])
                | _ -> ()
            elif !inProjectBlock then
                // DSL body
                if trimmed.StartsWith("namespace ", StringComparison.OrdinalIgnoreCase) then
                    rootNamespace <- Some (trimmed.["namespace ".Length..].Trim())
                elif trimmed.StartsWith("global import ", StringComparison.OrdinalIgnoreCase) then
                    implicitImports.Add(trimmed.["global import ".Length..].Trim())
                elif trimmed.StartsWith("files ", StringComparison.OrdinalIgnoreCase) ||
                     (trimmed.StartsWith("files") && trimmed.Contains("=")) then
                    let value = trimmed.Substring(trimmed.IndexOf('=') + 1).Trim()
                    files.AddRange(parseStringList value)
                elif trimmed.StartsWith("exclude ", StringComparison.OrdinalIgnoreCase) ||
                     (trimmed.StartsWith("exclude") && trimmed.Contains("=")) then
                    let value = trimmed.Substring(trimmed.IndexOf('=') + 1).Trim()
                    exclude.AddRange(parseStringList value)
                elif trimmed.StartsWith("source_roots") && trimmed.Contains("=") then
                    let value = trimmed.Substring(trimmed.IndexOf('=') + 1).Trim()
                    sourceRoots <- parseStringList value

        { ProjectRoot = projectRoot
          RootNamespace = rootNamespace
          SourceRoots = sourceRoots :> IReadOnlyList<_>
          Files = files :> IReadOnlyList<_>
          Exclude = exclude :> IReadOnlyList<_>
          ImplicitImports = implicitImports :> IReadOnlyList<_> }

module DashSpecProjectLocator =

    /// <summary>Project file = `*.dsproj` (ADR-0101). One per package directory; no legacy fallback.</summary>
    let private findProjectFile (full: string) =
        Directory.EnumerateFiles(full, "*.dsproj")
        |> Seq.sort
        |> Seq.tryHead

    let tryFindConfig (specDirectory: string option) =
        match specDirectory with
        | None | Some "" -> None
        | Some dir ->
            let full = Path.GetFullPath dir
            findProjectFile full |> Option.map DashSpecProjectConfig.load

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
