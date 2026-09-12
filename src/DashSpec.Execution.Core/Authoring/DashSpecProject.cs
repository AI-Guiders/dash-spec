using AIGuiders.Platform.Authoring.Core;

namespace DashSpec.Execution.Authoring;

/// <summary>Open a dashspec workspace via federation <see cref="AuthoringProjectLoader"/>.</summary>
public static class DashSpecProject
{
    public static DashSpecProjectResult Open(string workspaceRoot, string dashspecPath)
    {
        var load = AuthoringProjectLoader.OpenSingleFile(workspaceRoot, dashspecPath);
        if (load.Project is null)
        {
            return new() { Diagnostics = load.Diagnostics };
        }

        var project = AuthoringProjectGraph.ExpandLogicalImports(load.Project, ResolveReferencePaths);
        return new() { Project = project, Diagnostics = load.Diagnostics };
    }

    static IEnumerable<string> ResolveReferencePaths(string baseDirectory, string reference)
    {
        if (reference.Contains('*', StringComparison.Ordinal))
        {
            foreach (var path in ResolveGlob(reference, baseDirectory))
            {
                yield return path;
            }

            yield break;
        }

        yield return SpecFragmentPaths.ResolveExistingFragmentPath(reference, baseDirectory);
    }

    static IEnumerable<string> ResolveGlob(string reference, string specDirectory)
    {
        var combined = Path.GetFullPath(Path.Combine(specDirectory, reference));
        var directory = Path.GetDirectoryName(combined);
        var pattern = Path.GetFileName(combined);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(pattern))
        {
            yield break;
        }

        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var path in Directory.GetFiles(directory, pattern).OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
        {
            yield return path;
        }
    }
}

public sealed class DashSpecProjectResult
{
    public AuthoringProject? Project { get; init; }

    public IReadOnlyList<AuthoringDiagnostic> Diagnostics { get; init; } = [];
}
