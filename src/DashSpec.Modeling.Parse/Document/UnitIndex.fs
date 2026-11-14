namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open System.IO
open DashSpec.Modeling.Parse.Project

/// <summary>One unit of the resolution index: file, declared namespace and module kind keyword
/// (`None` for a mixed module without a kind) — ADR-0098, ADR-0101.</summary>
type UnitIndexEntry =
    { Path: string
      Namespace: string
      Kind: string option
      ModuleId: string option }

/// <summary>Namespace and module-id resolution over the project roots (ADR-0098 / ADR-0131). Built once per compile from the project
/// membership (<c>.dsproj</c>) plus the product stdlib, so imports and unit references no longer rescan the tree per directive.</summary>
type UnitIndex(entries: IReadOnlyList<UnitIndexEntry>) =

    let byNamespace = Dictionary<string, ResizeArray<UnitIndexEntry>>(StringComparer.OrdinalIgnoreCase)

    do
        for entry in entries do
            if not (String.IsNullOrWhiteSpace entry.Namespace) then
                match byNamespace.TryGetValue entry.Namespace with
                | true, bucket -> bucket.Add entry
                | false, _ ->
                    let bucket = ResizeArray<UnitIndexEntry>()
                    bucket.Add entry
                    byNamespace.[entry.Namespace] <- bucket

    member _.Entries = entries

    /// <summary>Units whose header declares the namespace; empty when the namespace is unknown.</summary>
    member _.Find(namespaceName: string) : UnitIndexEntry list =
        match byNamespace.TryGetValue namespaceName with
        | true, bucket -> List.ofSeq bucket
        | false, _ -> []

    /// <summary>Find units for a given namespace and kind keyword (e.g. `diagrams` / `diagram`),
    /// matching either exact <c>&lt;Namespace&gt;</c> or child <c>&lt;Namespace&gt;.&lt;keyword&gt;</c> / <c>&lt;Namespace&gt;.&lt;unitKeyword&gt;</c>.</summary>
    member this.FindForKind(namespaceName: string, keyword: string, unitKeyword: string) : UnitIndexEntry list =
        let candidates =
            [ yield! this.Find namespaceName
              if not (String.IsNullOrWhiteSpace keyword) then
                  yield! this.Find $"{namespaceName}.{keyword}"
              if not (String.IsNullOrWhiteSpace unitKeyword)
                 && not (String.Equals(keyword, unitKeyword, StringComparison.OrdinalIgnoreCase)) then
                  yield! this.Find $"{namespaceName}.{unitKeyword}" ]

        let seen = HashSet<string>(StringComparer.OrdinalIgnoreCase)
        [ for entry in candidates do
              let kindMatches =
                  match entry.Kind with
                  | None -> true
                  | Some k -> String.Equals(k, unitKeyword, StringComparison.OrdinalIgnoreCase)
              if kindMatches && seen.Add entry.Path then
                  yield entry ]

    /// <summary>Find a unit by kind (`catalog`, `dashboard`, …) and either its module id (`module &lt;kind&gt; &lt;id&gt;`) or namespace.</summary>
    member this.TryFindUnit(kindKeyword: string, idOrNamespace: string) : UnitIndexEntry option =
        if String.IsNullOrWhiteSpace idOrNamespace then
            None
        else
            let needle = idOrNamespace.Trim()
            let matchesKind (e: UnitIndexEntry) =
                match e.Kind with
                | Some k -> String.Equals(k, kindKeyword, StringComparison.OrdinalIgnoreCase)
                | None -> false

            entries
            |> Seq.tryFind (fun e ->
                matchesKind e
                && (match e.ModuleId with
                    | Some mid -> String.Equals(mid, needle, StringComparison.OrdinalIgnoreCase)
                    | None -> false))
            |> Option.orElseWith (fun () ->
                this.FindForKind(needle, kindKeyword + "s", kindKeyword) |> List.tryHead)

module UnitIndex =

    let private enumerateUnitFiles (directory: string) =
        if Directory.Exists directory then
            Directory.EnumerateFiles(directory, "*.ds", SearchOption.AllDirectories)
            |> Seq.toList
        else
            []

    /// Project membership when the project declares files, its source roots otherwise, the spec directory as fallback.
    let private projectFiles (specDirectory: string) =
        match DashSpecProjectLocator.tryFindConfig (Some specDirectory) with
        | Some cfg when cfg.Files.Count > 0 -> DashSpecProjectMembership.expand cfg |> List.ofSeq
        | Some cfg ->
            cfg.SourceRoots
            |> Seq.collect (fun root -> enumerateUnitFiles (Path.GetFullPath(Path.Combine(cfg.ProjectRoot, root))))
            |> Seq.toList
        | None -> enumerateUnitFiles (Path.GetFullPath specDirectory)

    let private candidateFiles (specDirectory: string) =
        projectFiles specDirectory @ UnitSource.embeddedUnitPaths ()

    /// Corpus member files — dsproj membership when declared, its source roots
    /// otherwise, the spec directory scan as fallback. No stdlib: the assembled
    /// corpus stamps its own members (the stdlib is product-shipped static content;
    /// a changed stdlib rides a product restart, not a mid-process mtime).
    let corpusFiles (specDirectory: string) : string[] =
        projectFiles specDirectory |> List.toArray

    /// Raw text — the index reads headers (namespace/kind) that readUnit blanks out.
    let private readEntry (path: string) =
        try
            match UnitSource.tryReadRaw path with
            | Some text ->
                let nsOpt = UnitSource.namespaceOf text
                let kindOpt = UnitSource.moduleKindOf text
                let idOpt = UnitSource.moduleIdOf text
                if nsOpt.IsSome || idOpt.IsSome then
                    Some
                        { Path = path
                          Namespace = nsOpt |> Option.defaultValue ""
                          Kind = kindOpt
                          ModuleId = idOpt }
                else
                    None
            | None -> None
        with _ ->
            None

    /// <summary>Index of the units reachable from a spec file: project membership (or its directory) plus the embedded stdlib.</summary>
    let build (specDirectory: string) : UnitIndex =
        let entries =
            candidateFiles specDirectory
            |> List.filter UnitSource.existsUnit
            |> List.distinctBy (fun path -> path.ToLowerInvariant())
            |> List.choose readEntry
            |> List.toArray

        UnitIndex(entries :> IReadOnlyList<_>)
