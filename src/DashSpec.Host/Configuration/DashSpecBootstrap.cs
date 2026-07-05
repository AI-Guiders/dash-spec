using DashSpec.Core.Parsing;

namespace DashSpec.Host.Configuration;

/// <summary>Host bootstrap: dash-spec.toml → catalog → default entry @runtime TOML.</summary>
public static class DashSpecBootstrap
{
    public static DashSpecTomlRoot LoadBootstrap(IHostEnvironment environment)
    {
        var contentRoot = environment.ContentRootPath;
        var bootstrapPath = Path.Combine(contentRoot, "dash-spec.toml");
        if (!File.Exists(bootstrapPath))
        {
            throw new InvalidOperationException(
                "Host requires dash-spec.toml with [dashboard] catalog_path pointing to a .dashcatalog file.");
        }

        var bootstrap = DashSpecTomlLoader.LoadFile(bootstrapPath);
        bootstrap = OverlayOptionalToml(bootstrap, Path.Combine(contentRoot, "dash-spec.dev.toml"));
        bootstrap = OverlayOptionalToml(bootstrap, Path.Combine(contentRoot, "dash-spec.local.toml"));

        var envCatalogPath = Environment.GetEnvironmentVariable("DASHSPEC_CATALOG_PATH");
        if (!string.IsNullOrWhiteSpace(envCatalogPath))
        {
            bootstrap.Dashboard.CatalogPath = envCatalogPath;
        }

        if (string.IsNullOrWhiteSpace(bootstrap.Dashboard.CatalogPath))
        {
            throw new InvalidOperationException(
                "dash-spec.toml: set [dashboard] catalog_path to your .dashcatalog file.");
        }

        ApplyAccessEnvOverride(bootstrap);

        return bootstrap;
    }

    public static CatalogBootstrap LoadCatalog(DashSpecTomlRoot bootstrap, string contentRoot)
    {
        var catalogPath = ResolveCatalogPath(contentRoot, bootstrap.Dashboard.CatalogPath);
        return new CatalogBootstrap(CatalogParser.ParseFile(catalogPath), catalogPath);
    }

    public static string ResolveActiveSpecFullPath(
        CatalogBootstrap catalog,
        string? catalogEntryId = null)
    {
        var entryId = string.IsNullOrWhiteSpace(catalogEntryId)
            ? catalog.Document.DefaultEntryId
            : catalogEntryId;
        return catalog.ResolveEntrySpecFullPath(entryId);
    }

    public static string ToHostSpecReference(string contentRoot, string specFullPath)
    {
        var normalizedRoot = Path.GetFullPath(contentRoot);
        var normalizedSpec = Path.GetFullPath(specFullPath);
        if (normalizedSpec.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetRelativePath(normalizedRoot, normalizedSpec).Replace('\\', '/');
        }

        return normalizedSpec.Replace('\\', '/');
    }

    public static DashSpecAccessOptions LoadAccessOptions(IHostEnvironment environment)
    {
        var bootstrap = LoadBootstrap(environment);
        return new DashSpecAccessOptions { ApiKey = bootstrap.Access.ApiKey };
    }

    private static void ApplyAccessEnvOverride(DashSpecTomlRoot bootstrap)
    {
        var envKey = Environment.GetEnvironmentVariable("DASHSPEC_API_KEY");
        if (!string.IsNullOrWhiteSpace(envKey))
        {
            bootstrap.Access.ApiKey = envKey;
        }
    }

    private static DashSpecTomlRoot OverlayOptionalToml(DashSpecTomlRoot root, string path)
    {
        if (!File.Exists(path))
        {
            return root;
        }

        return DashSpecTomlLoader.Merge(root, DashSpecTomlLoader.LoadFile(path));
    }

    public static DashSpecTomlRoot Load(IHostEnvironment environment)
    {
        var bootstrap = LoadBootstrap(environment);
        var catalog = LoadCatalog(bootstrap, environment.ContentRootPath);
        var specPath = ResolveActiveSpecFullPath(catalog);
        if (!File.Exists(specPath))
        {
            throw new FileNotFoundException("DashSpec file not found.", specPath);
        }

        var specText = File.ReadAllText(specPath);
        var configPath = ResolveRuntimeConfigPath(specPath, specText);
        var runtime = DashSpecTomlLoader.LoadFile(configPath);
        runtime.Dashboard.CatalogPath = bootstrap.Dashboard.CatalogPath;
        ValidateRuntimeConfig(runtime, configPath);
        return runtime;
    }

    public static string ResolveSpecLibraryPath(
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

    public static string ResolveRuntimeConfigPath(
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

    private static void ValidateRuntimeConfig(DashSpecTomlRoot root, string configPath)
    {
        if (root.Plugins.Load.Count == 0)
        {
            throw new InvalidOperationException(
                $"Config '{configPath}' must define [[plugins.load]] entries.");
        }

        if (root.Connectors.Values.All(section => string.IsNullOrWhiteSpace(section.ConnectionString)))
        {
            throw new InvalidOperationException(
                $"Config '{configPath}' must define at least one [connectors.*] connection_string.");
        }
    }

    public static string ResolveSpecPath(string contentRoot, string specPath) =>
        SpecPathResolver.ResolveFromContentRoot(contentRoot, specPath);

    public static string ResolveCatalogPath(string contentRoot, string catalogPath)
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
}
