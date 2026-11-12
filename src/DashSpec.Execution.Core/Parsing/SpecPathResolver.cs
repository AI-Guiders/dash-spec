namespace DashSpec.Execution.Parsing;

public static class SpecPathResolver
{
    public static string ResolveNearSpec(
        string specFullPath,
        string relativePath,
        string? fallbackDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(specFullPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var searchDirs = new List<string> { Path.GetDirectoryName(specFullPath)! };
        if (!string.IsNullOrWhiteSpace(fallbackDirectory))
        {
            searchDirs.Add(fallbackDirectory);
        }

        foreach (var dir in searchDirs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Directory.Exists(dir))
            {
                var unitHit = DashSpec.Modeling.Parse.Document.UnitIndexModule.build(dir).TryFindUnit("runtime", relativePath);
                if (unitHit is not null && File.Exists(unitHit.Value.Path))
                {
                    return Path.GetFullPath(unitHit.Value.Path);
                }
            }

            var candidate = Path.GetFullPath(Path.Combine(dir, relativePath));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var primary = Path.GetFullPath(Path.Combine(searchDirs[0], relativePath));
        throw new FileNotFoundException(
            $"Spec asset not found: '{relativePath}' (searched near {specFullPath}).",
            primary);
    }

    public static string ResolveFromContentRoot(string contentRoot, string specPath) =>
        Path.IsPathRooted(specPath)
            ? specPath
            : Path.GetFullPath(Path.Combine(contentRoot, specPath));
}
