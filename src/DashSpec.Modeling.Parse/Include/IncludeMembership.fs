namespace DashSpec.Modeling.Parse.Include

open System
open System.Collections.Generic
open System.IO
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

/// Project-style membership: expand globs to a deterministic path set (slnx / Compile Include).
module IncludeMembership =

    let isIncomplete (reference: string) =
        if String.IsNullOrWhiteSpace reference then true
        elif reference.Contains '*' then false
        else reference.EndsWith "/" || reference.EndsWith "\\"

    let resolveExistingIncludePath (reference: string) (specDirectory: string) =
        let path = SpecIncludeResolver.resolvePath reference specDirectory

        if File.Exists path then path
        else
            let extensions =
                [| ".dashlayout"
                   ".dashdiagram"
                   ".dashinclude"
                   ".dashpresentation"
                   ".dashtooltip"
                   ".dashtype"
                   ".dashflow" |]

            let mutable resolved = path

            for ext in extensions do
                let withExt =
                    if path.EndsWith(ext, StringComparison.OrdinalIgnoreCase) then path
                    else path + ext

                if File.Exists withExt then resolved <- withExt

            resolved

    let resolvePaths (reference: string) (specDirectory: string) =
        if not (reference.Contains '*') then
            seq { yield resolveExistingIncludePath reference specDirectory }
        else
            let combined = Path.GetFullPath(Path.Combine(specDirectory, reference))
            let directory = Path.GetDirectoryName combined

            if String.IsNullOrWhiteSpace directory then
                raise (DashSpecParseException($"Include glob has no directory: '{reference}'."))

            let pattern = Path.GetFileName combined

            if String.IsNullOrWhiteSpace pattern then
                raise (DashSpecParseException($"Include glob requires a file pattern: '{reference}'."))

            if not (Directory.Exists directory) then
                Seq.empty
            else
                Directory.GetFiles(directory, pattern)
                |> Array.sortWith (fun left right -> String.Compare(left, right, StringComparison.OrdinalIgnoreCase))
                |> Seq.ofArray

    let private collectDashIncludePaths
        (path: string)
        (specDirectory: string)
        (tolerateIncompleteIncludes: bool)
        (acc: HashSet<string>)
        =
        if not (File.Exists path) then
            raise (FileNotFoundException($"Include not found: '{path}'.", path))

        let reader = ParserUtilities.createReader (File.ReadAllText path)
        reader.SkipNewlines()

        if reader.IsAt TokenKind.At then
            reader.Advance()

            if reader.TryKeyword "include" then
                reader.ReadIdent() |> ignore
                reader.SkipNewlines()

        let bundleDir =
            Path.GetDirectoryName path |> Option.ofObj |> Option.defaultValue specDirectory

        while not reader.IsEof do
            match reader.TryModuleInclude() with
            | Some nested ->
                if not (tolerateIncompleteIncludes && isIncomplete nested) then
                    for resolved in resolvePaths nested bundleDir do
                        acc.Add resolved |> ignore

                reader.SkipNewlines()
            | None ->
                if reader.TryKeyword "layout" then
                    let layoutReference = reader.ReadString()

                    let layoutPath =
                        layoutReference
                        |> fun reference -> SpecIncludeResolver.resolvePath reference bundleDir
                        |> fun p ->
                            if File.Exists p then p
                            elif File.Exists(p + ".dashlayout") then p + ".dashlayout"
                            else p

                    acc.Add layoutPath |> ignore
                    reader.SkipNewlines()
                elif reader.TryKeyword "diagram" then
                    let diagramReference = reader.ReadString()

                    let diagramPath =
                        diagramReference
                        |> fun reference -> SpecIncludeResolver.resolvePath reference bundleDir
                        |> fun p ->
                            if File.Exists p then p
                            elif File.Exists(p + ".dashdiagram") then p + ".dashdiagram"
                            else p

                    acc.Add diagramPath |> ignore
                    reader.SkipNewlines()
                else
                    raise (reader.Unexpected())

    let collectUnitPaths
        (directives: IReadOnlyList<ModuleLinkDirective>)
        (specDirectory: string)
        (tolerateIncompleteIncludes: bool)
        =
        let acc = HashSet<string>(StringComparer.OrdinalIgnoreCase)

        for directive in directives do
            match directive with
            | ModuleLinkDirective.PathReference reference ->
                if tolerateIncompleteIncludes && isIncomplete reference then ()
                else
                    for path in resolvePaths reference specDirectory do
                        acc.Add path |> ignore
            | ModuleLinkDirective.DiagramFrom(_, path) ->
                if tolerateIncompleteIncludes && isIncomplete path then ()
                else
                    let resolved = resolveExistingIncludePath path specDirectory
                    acc.Add resolved |> ignore

        let mutable expanded = true

        while expanded do
            expanded <- false
            let snapshot = acc |> Seq.toArray

            for path in snapshot do
                if path.EndsWith(".dashinclude", StringComparison.OrdinalIgnoreCase) then
                    let before = acc.Count
                    collectDashIncludePaths path specDirectory tolerateIncompleteIncludes acc
                    if acc.Count <> before then expanded <- true

        acc
            |> Seq.sortWith (fun left right -> String.Compare(left, right, StringComparison.OrdinalIgnoreCase))
            |> Seq.toList
