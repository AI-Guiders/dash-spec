using DashSpec.Core.Parsing;
using DashSpec.Host.Services.Abstractions;
using DashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;

namespace DashSpec.Host.Configuration;

public sealed class HostPathResolver : IHostPathResolver
{
    public string ResolveSpecPath(string contentRoot, string specPath) =>
        SpecPathResolver.ResolveFromContentRoot(contentRoot, specPath);

    public string ResolveCatalogPath(string contentRoot, string catalogPath)
    {
        var path = SpecPathResolver.ResolveFromContentRoot(contentRoot, catalogPath);
        if (File.Exists(path))
        {
            return path;
        }

        const string extension = ".dashcatalog";
        var withExt = path.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? path : path + extension;
        if (!File.Exists(withExt))
        {
            throw new FileNotFoundException("Catalog file not found.", withExt);
        }

        return withExt;
    }

    public string? ResolveDashhostPath(string contentRoot, string? dashhostReference) =>
        ResolveDashhostFile(contentRoot, dashhostReference);

    public string ToHostSpecReference(string contentRoot, string specFullPath)
    {
        var normalizedRoot = Path.GetFullPath(contentRoot);
        var normalizedSpec = Path.GetFullPath(specFullPath);
        if (normalizedSpec.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetRelativePath(normalizedRoot, normalizedSpec).Replace('\\', '/');
        }

        return normalizedSpec.Replace('\\', '/');
    }

    public string ResolveSpecLibraryPath(
        string specFullPath,
        string? libraryRelative,
        string? libraryFallbackDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(libraryRelative))
        {
            return string.Empty;
        }

        return SpecPathResolver.ResolveNearSpec(specFullPath, libraryRelative, libraryFallbackDirectory);
    }

    public string ResolveRuntimeConfigPath(
        string specFullPath,
        string specText,
        string? configFallbackDirectory = null)
    {
        var configRelative = DashSpecParser.ReadRuntimePath(specText);
        if (string.IsNullOrWhiteSpace(configRelative))
        {
            throw new InvalidOperationException(
                """
                В .dashspec нет runtime { manifest = … } — укажите в блоке @dashboard/@tab, например:
                  runtime { manifest = "demo.toml" }
                Файл runtime (TOML) должен содержать [connectors.*] и [plugins].
                """);
        }

        return SpecPathResolver.ResolveNearSpec(specFullPath, configRelative, configFallbackDirectory);
    }

    private static string? ResolveDashhostFile(string contentRoot, string? dashhostReference)
    {
        if (string.IsNullOrWhiteSpace(dashhostReference))
        {
            return null;
        }

        var path = SpecPathResolver.ResolveFromContentRoot(contentRoot, dashhostReference);
        if (File.Exists(path))
        {
            return path;
        }

        const string extension = ".dashhost";
        var withExt = path.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? path : path + extension;
        return File.Exists(withExt) ? withExt : null;
    }
}
