namespace DashSpec.Modeling.Parse.Document

open System
open System.IO
open System.Text.RegularExpressions

/// <summary>Unit files (ADR-0101): one unit per file, `module &lt;kind&gt; &lt;id&gt;`, header lines `namespace` and `import` directly after it (ADR-0098).</summary>
module UnitSource =

    let private namespaceLine = Regex(@"^\s*namespace\s+([A-Za-z_][\w.]*)\s*$", RegexOptions.Compiled)

    let private isHeaderLine (line: string) =
        let trimmed = line.Trim()

        trimmed = ""
        || trimmed.StartsWith("namespace ", StringComparison.Ordinal)
        || trimmed.StartsWith("import ", StringComparison.Ordinal)

    let private moduleIndex (lines: string[]) =
        lines |> Array.tryFindIndex (fun line -> line.TrimStart().StartsWith("module ", StringComparison.Ordinal))

    /// Header lines follow the `module` line. Unit parsers see them as blanks of equal length, so offsets stay put.
    let blankHeader (text: string) =
        let lines = text.Split('\n')

        match moduleIndex lines with
        | None -> text
        | Some start ->
            let headerEnd =
                start + 1 + (lines |> Array.skip (start + 1) |> Array.takeWhile isHeaderLine |> Array.length)

            lines
            |> Array.mapi (fun index line -> if index > start && index < headerEnd then String.replicate line.Length " " else line)
            |> String.concat "\n"

    let readUnit (path: string) = blankHeader (File.ReadAllText path)

    /// Namespace declared in the header of a unit file, if any.
    let namespaceOf (text: string) : string option =
        let lines = text.Split('\n')

        match moduleIndex lines with
        | None -> None
        | Some start ->
            lines
            |> Array.skip (start + 1)
            |> Array.takeWhile isHeaderLine
            |> Array.tryPick (fun line ->
                let m = namespaceLine.Match(line)
                if m.Success then Some m.Groups.[1].Value else None)

    /// Namespace shipped with the product: the stdlib folder (ADR-0017).
    [<Literal>]
    let StandardNamespace = "DashSpec.Std"

    /// Unit files under the spec directory and the product stdlib whose header declares the namespace (namespace scan, no index).
    let findByNamespace (specDirectory: string) (namespaceName: string) : string list =
        [ specDirectory; DashSpec.Modeling.Parse.Include.SpecFragmentPaths.getStdlibRoot () ]
        |> List.collect (fun root -> Directory.EnumerateFiles(root, "*.ds", SearchOption.AllDirectories) |> Seq.toList)
        |> List.filter (fun path ->
            try
                match namespaceOf (File.ReadAllText path) with
                | Some ns -> String.Equals(ns, namespaceName, StringComparison.OrdinalIgnoreCase)
                | None -> false
            with _ ->
                false)
