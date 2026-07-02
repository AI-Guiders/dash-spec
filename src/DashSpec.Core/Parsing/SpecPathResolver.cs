namespace DashSpec.Core.Parsing;

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
