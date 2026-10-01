using Microsoft.FSharp.Core;

namespace DashSpec.Core.Authoring;

/// <summary>
/// Resolve fragment include paths for project graph expansion and parse-time includes.
/// SSOT: <c>DashSpec.Modeling.Parse.Include.SpecFragmentPaths</c> (F#).
/// </summary>
public static class SpecFragmentPaths
{
    public static void SetStdlibRootForTests(string? path) =>
        Modeling.Parse.Include.SpecFragmentPaths.setStdlibRootForTests(
            string.IsNullOrWhiteSpace(path) ? FSharpOption<string>.None : FSharpOption<string>.Some(path));

    public static string ResolvePath(string reference, string specDirectory) =>
        Modeling.Parse.Include.SpecFragmentPaths.resolvePath(reference, specDirectory);

    public static string ResolveExistingFragmentPath(string reference, string specDirectory) =>
        Modeling.Parse.Include.SpecFragmentPaths.resolveExistingFragmentPath(reference, specDirectory);
}
