using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

public static class CatalogParser
{
    public static CatalogDocument Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (CatalogParseBridge.Parse is { } parse)
        {
            return parse(text);
        }

        throw new InvalidOperationException("Catalog parse bridge not registered.");
    }

    public static CatalogDocument ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Catalog file not found.", path);
        }

        return Parse(File.ReadAllText(path));
    }

    public static string ResolveEntrySpecPath(string catalogFullPath, string dashspecReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogFullPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(dashspecReference);

        var catalogDir = Path.GetDirectoryName(catalogFullPath)!;
        var searchDirs = new[]
        {
            catalogDir,
            Path.GetFullPath(Path.Combine(catalogDir, "..")),
        };

        foreach (var dir in searchDirs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var path = SpecIncludeResolver.ResolvePath(dashspecReference, dir);
            if (!path.EndsWith(".dashspec", StringComparison.OrdinalIgnoreCase))
            {
                path += ".dashspec";
            }

            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException(
            $"Catalog entry dashspec not found: '{dashspecReference}' (catalog: {catalogFullPath}).",
            dashspecReference);
    }
}
