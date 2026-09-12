namespace DashSpec.Execution.Authoring;

/// <summary>Resolve fragment include paths for project graph expansion and parse-time includes.</summary>
public static class SpecFragmentPaths
{
    private static string? _stdlibRootOverride;

    public static void SetStdlibRootForTests(string? path) => _stdlibRootOverride = path;

    public static string ResolvePath(string reference, string specDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(specDirectory);

        if (IsStdlibReference(reference))
        {
            var inner = reference[1..^1].Trim().Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(GetStdlibRoot(), inner);
        }

        return Path.IsPathRooted(reference)
            ? reference
            : Path.GetFullPath(Path.Combine(specDirectory, reference));
    }

    public static string ResolveExistingFragmentPath(string reference, string specDirectory)
    {
        var path = ResolvePath(reference, specDirectory);
        if (File.Exists(path))
        {
            return path;
        }

        foreach (var ext in new[] { ".dashlayout", ".dashdiagram", ".dashinclude", ".dashpresentation", ".dashtooltip" })
        {
            var withExt = path.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? path : path + ext;
            if (File.Exists(withExt))
            {
                return withExt;
            }
        }

        return path;
    }

    private static bool IsStdlibReference(string reference) =>
        reference.Length >= 2 && reference[0] is '<' && reference[^1] is '>';

    private static string GetStdlibRoot()
    {
        if (!string.IsNullOrWhiteSpace(_stdlibRootOverride))
        {
            return _stdlibRootOverride;
        }

        var assemblyDir = Path.GetDirectoryName(typeof(SpecFragmentPaths).Assembly.Location);
        if (!string.IsNullOrWhiteSpace(assemblyDir))
        {
            var nextToAssembly = Path.Combine(assemblyDir, "stdlib");
            if (Directory.Exists(nextToAssembly))
            {
                return nextToAssembly;
            }
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "stdlib");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            var coreCandidate = Path.Combine(dir.FullName, "src", "DashSpec.Core", "stdlib");
            if (Directory.Exists(coreCandidate))
            {
                return coreCandidate;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("DashSpec stdlib directory was not found.");
    }
}
