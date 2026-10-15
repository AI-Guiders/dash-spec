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
            var path = Path.GetFullPath(Path.Combine(dir, dashspecReference));
            if (File.Exists(path))
            {
                return path;
            }

            var withDs = path.EndsWith(".ds", StringComparison.OrdinalIgnoreCase) ? path : path + ".ds";
            if (File.Exists(withDs))
            {
                return withDs;
            }
        }

        throw new FileNotFoundException(
            $"Catalog entry dashspec not found: '{dashspecReference}' (catalog: {catalogFullPath}).",
            dashspecReference);
    }
}
