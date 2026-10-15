namespace DashSpec.Modeling.Parse.Document

open System
open System.IO
open System.Reflection
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

    /// Module id of a unit file: the third word of the `module <kind> <id>` line.
    let moduleIdOf (text: string) : string option =
        let lines = text.Split('\n')

        match moduleIndex lines with
        | None -> None
        | Some start ->
            let parts = lines.[start].Trim().Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)
            if parts.Length >= 3 then Some parts.[2] else None

    /// Unit kind keyword of a unit file (`module <kind> <id>`), or None for a mixed module (`module <name>`).
    let moduleKindOf (text: string) : string option =
        let lines = text.Split('\n')

        match moduleIndex lines with
        | None -> None
        | Some start ->
            let parts = lines.[start].Trim().Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)

            if
                parts.Length >= 2
                && (DashSpec.Modeling.Core.ImportKindRegistry.tryFindByModuleKeyword parts.[1]).IsSome
            then
                Some(parts.[1].ToLowerInvariant())
            else
                None

    /// Kind and id of a unit header in a C#-friendly shape: empty strings when the file declares neither.
    let tryKindAndId (text: string) : string * string =
        let lines = text.Split('\n')

        match moduleIndex lines with
        | None -> "", ""
        | Some start ->
            let parts = lines.[start].Trim().Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)

            // Raw module keyword: `host`, `catalog`, `dashboard` and `tab` are module roots too,
            // so dispatch by kind must not be limited to the importable kinds.
            if parts.Length >= 3 then parts.[1].ToLowerInvariant(), parts.[2] else "", ""

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

    let mutable private stdlibRootOverride: string option = None

    let setStdlibRootForTests (path: string option) = stdlibRootOverride <- path

    let stdlibRoot () : string =
        match stdlibRootOverride with
        | Some path when not (String.IsNullOrWhiteSpace path) -> path
        | _ ->
            let assemblyDir =
                Path.GetDirectoryName(Reflection.Assembly.GetExecutingAssembly().Location)
            if not (String.IsNullOrWhiteSpace assemblyDir) then
                let nextToAssembly = Path.Combine(assemblyDir, "stdlib")
                if Directory.Exists nextToAssembly then nextToAssembly
                else
                    let dir = DirectoryInfo(AppContext.BaseDirectory)
                    let mutable current: DirectoryInfo option = Some dir
                    let mutable found = None
                    while current.IsSome && found.IsNone do
                        let d = current.Value
                        let candidate = Path.Combine(d.FullName, "stdlib")
                        if Directory.Exists candidate then
                            found <- Some candidate
                        else
                            let coreCandidate = Path.Combine(d.FullName, "src", "DashSpec.Core", "stdlib")
                            if Directory.Exists coreCandidate then
                                found <- Some coreCandidate
                            else
                                current <- if isNull d.Parent then None else Some d.Parent
                    match found with
                    | Some path -> path
                    | None -> raise (InvalidOperationException("DashSpec stdlib directory was not found."))
            else
                raise (InvalidOperationException("DashSpec stdlib directory was not found."))
