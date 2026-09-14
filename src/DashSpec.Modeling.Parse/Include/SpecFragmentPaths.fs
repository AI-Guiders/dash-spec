namespace DashSpec.Modeling.Parse.Include

open System
open System.IO
open System.Reflection

/// Resolve fragment include paths for parse-time includes.
module SpecFragmentPaths =

    let mutable stdlibRootOverride = None

    let setStdlibRootForTests (path: string option) = stdlibRootOverride <- path

    let private isStdlibReference (reference: string) =
        reference.Length >= 2 && reference.[0] = '<' && reference.[reference.Length - 1] = '>'

    let private getStdlibRoot () =
        match stdlibRootOverride with
        | Some path when not (String.IsNullOrWhiteSpace path) -> path
        | _ ->
            let assemblyDir =
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
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

    let resolvePath (reference: string) (specDirectory: string) =
        if String.IsNullOrWhiteSpace reference then
            invalidArg "reference" "Reference is required."
        if String.IsNullOrWhiteSpace specDirectory then
            invalidArg "specDirectory" "Spec directory is required."

        if isStdlibReference reference then
            let inner = reference.Substring(1, reference.Length - 2).Trim().Replace('/', Path.DirectorySeparatorChar)
            Path.Combine(getStdlibRoot(), inner)
        elif Path.IsPathRooted reference then reference
        else Path.GetFullPath(Path.Combine(specDirectory, reference))

    let resolveExistingFragmentPath (reference: string) (specDirectory: string) =
        let path = resolvePath reference specDirectory
        if File.Exists path then path
        else
            let extensions =
                [| ".dashlayout"; ".dashdiagram"; ".dashinclude"; ".dashpresentation"; ".dashtooltip" |]
            let mutable resolved = path
            for ext in extensions do
                let withExt =
                    if path.EndsWith(ext, StringComparison.OrdinalIgnoreCase) then path
                    else path + ext
                if File.Exists withExt then
                    resolved <- withExt
            resolved
