
namespace DashSpec.Execution.Parsing;

public static class CatalogReader
{
    public static CatalogDocument Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return ModuleParseRegistration.ParseCatalog(text);
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
            if (Directory.Exists(dir))
            {
                var idx = DashSpec.Modeling.Parse.Document.UnitIndexModule.build(dir);
                var unitHit = idx.TryFindUnit("dashboard", dashspecReference)
                    ?? idx.TryFindUnit("section", dashspecReference);
                if (unitHit is not null && File.Exists(unitHit.Value.Path))
                {
                    return Path.GetFullPath(unitHit.Value.Path);
                }
            }

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
