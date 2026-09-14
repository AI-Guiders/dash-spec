namespace DashSpec.Modeling.Parse

open System
open System.IO

/// <summary>Resolve fragment include paths for parse-time includes.</summary>
module SpecIncludeResolver =

    let resolvePath (reference: string) (specDirectory: string) =
        if String.IsNullOrWhiteSpace reference then
            invalidArg "reference" "Include reference is required."
        if String.IsNullOrWhiteSpace specDirectory then
            invalidArg "specDirectory" "Spec directory is required."

        if reference.Length >= 2 && reference.[0] = '<' && reference.[reference.Length - 1] = '>' then
            invalidOp "Stdlib include references are not yet supported in F# Modeling.Parse."

        if Path.IsPathRooted reference then reference
        else Path.GetFullPath(Path.Combine(specDirectory, reference))

    let resolveLayoutFile (path: string) =
        if File.Exists path then path
        else
            let ext = ".dashlayout"
            let withExt =
                if path.EndsWith(ext, StringComparison.OrdinalIgnoreCase) then path
                else path + ext
            if File.Exists withExt then withExt else path
