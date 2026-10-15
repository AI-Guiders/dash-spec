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
      Kind: string option }

/// <summary>Namespace resolution over the project roots (ADR-0098). Built once per compile from the project
/// membership (<c>.dsproj</c>) plus the product stdlib, so imports no longer rescan the tree per directive.</summary>
type UnitIndex(entries: IReadOnlyList<UnitIndexEntry>) =

    let byNamespace = Dictionary<string, ResizeArray<UnitIndexEntry>>(StringComparer.OrdinalIgnoreCase)

    do
        for entry in entries do
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

module UnitIndex =

    let private enumerateUnitFiles (directory: string) =
        if Directory.Exists directory then
            Directory.EnumerateFiles(directory, "*.ds", SearchOption.AllDirectories)
            |> Seq.toList
        else
            []

    /// Project membership when the project declares files, its source roots otherwise, the spec directory as fallback.
    let private candidateFiles (specDirectory: string) =
        let projectFiles =
            match DashSpecProjectLocator.tryFindConfig (Some specDirectory) with
            | Some cfg when cfg.Files.Count > 0 -> DashSpecProjectMembership.expand cfg |> List.ofSeq
            | Some cfg ->
                cfg.SourceRoots
                |> Seq.collect (fun root -> enumerateUnitFiles (Path.GetFullPath(Path.Combine(cfg.ProjectRoot, root))))
                |> Seq.toList
            | None -> enumerateUnitFiles (Path.GetFullPath specDirectory)

        projectFiles @ enumerateUnitFiles (UnitSource.stdlibRoot ())

    let private readEntry (path: string) =
        try
            let text = File.ReadAllText path

            match UnitSource.namespaceOf text with
            | Some ns -> Some { Path = path; Namespace = ns; Kind = UnitSource.moduleKindOf text }
            | None -> None
        with _ ->
            None

    /// <summary>Index of the units reachable from a spec file: project membership (or its directory) plus the stdlib.</summary>
    let build (specDirectory: string) : UnitIndex =
        let entries =
            candidateFiles specDirectory
            |> List.filter File.Exists
            |> List.distinctBy (fun path -> path.ToLowerInvariant())
            |> List.choose readEntry
            |> List.toArray

        UnitIndex(entries :> IReadOnlyList<_>)
