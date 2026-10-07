namespace DashSpec.Modeling.Parse.Project

open System
open System.Collections.Generic
open System.IO
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Include

/// <summary>Resolve <c>import</c> directives via <c>dashspec.toml</c> index.</summary>
module DashSpecProjectIndex =

    let collectPathsForImport
        (project: DashSpecProjectConfig)
        (specDirectory: string)
        (kind: ImportKind)
        (namespaceName: string)
        (tolerateIncompleteIncludes: bool)
        =
        let matches =
            project.Index
            |> Seq.filter (fun entry ->
                entry.Kind = kind
                && String.Equals(entry.Namespace, namespaceName, StringComparison.OrdinalIgnoreCase))

        let paths = HashSet<string>(StringComparer.OrdinalIgnoreCase)

        for entry in matches do
            for path in IncludeMembership.resolvePaths entry.Glob specDirectory do
                if tolerateIncompleteIncludes && IncludeMembership.isIncomplete entry.Glob then ()
                elif File.Exists path then paths.Add path |> ignore

        if Seq.isEmpty matches then
            raise (
                DashSpecParseException(
                    $"No dashspec.toml index entry for import {ImportKindRegistry.keyword kind} from '{namespaceName}'."
                )
            )

        if paths.Count = 0 && not tolerateIncompleteIncludes then
            raise (
                DashSpecParseException(
                    $"Import {ImportKindRegistry.keyword kind} from '{namespaceName}' matched no files (glob under project root)."
                )
            )

        paths
        |> Seq.toArray
        |> Array.sortWith (fun left right -> String.Compare(left, right, StringComparison.OrdinalIgnoreCase))
