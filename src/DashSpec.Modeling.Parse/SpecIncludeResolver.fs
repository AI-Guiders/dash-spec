namespace DashSpec.Modeling.Parse

open DashSpec.Modeling.Parse.Include

/// <summary>Resolve fragment include paths for parse-time includes.</summary>
module SpecIncludeResolver =

    let resolvePath (reference: string) (specDirectory: string) =
        SpecFragmentPaths.resolvePath reference specDirectory

    let resolveLayoutFile (path: string) = SpecFragmentPaths.resolveLayoutFile path
