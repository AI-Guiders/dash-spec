using DashSpec.Core.Parsing;
using DashSpec.Host.Services.Settings;
using DashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;

namespace DashSpec.Host.Configuration;

/// <summary>Host bootstrap: ops TOML → <c>.dashhost</c> → catalog → default entry @runtime TOML.</summary>
public static class DashSpecBootstrap
{
    public static DashSpecTomlRoot LoadBootstrap(IHostEnvironment environment) =>
        LoadBootstrapWithHost(environment).Bootstrap;

    public static (DashSpecTomlRoot Bootstrap, HostShellBootstrap HostShell) LoadBootstrapWithHost(
        IHostEnvironment environment)
    {
        var contentRoot = environment.ContentRootPath;
        var bootstrapPath = Path.Combine(contentRoot, "dash-spec.toml");

        var bootstrap = File.Exists(bootstrapPath)
            ? DashSpecTomlLoader.LoadFile(bootstrapPath)
            : new DashSpecTomlRoot();

        bootstrap = OverlayOptionalToml(bootstrap, Path.Combine(contentRoot, "dash-spec.dev.toml"));
        bootstrap = OverlayOptionalToml(bootstrap, Path.Combine(contentRoot, "dash-spec.local.toml"));

        HostBootstrapPlanetTomlGuard.RejectLegacyPlanetToml(bootstrap);

        if (string.IsNullOrWhiteSpace(bootstrap.Host.Dashhost))
        {
            throw new InvalidOperationException(
                "[host] dashhost is required in dash-spec.toml or dash-spec.local.toml.");
        }

        var dashhostPath = ResolveDashhostPath(contentRoot, bootstrap.Host.Dashhost)
            ?? throw new FileNotFoundException(
                $"Host dashhost not found: '{bootstrap.Host.Dashhost}' (content root: {contentRoot}).");

        DashSpecParser.EnsureModuleParsersRegistered();
        var document = HostModuleParser.ParseFile(dashhostPath);
        var hostShell = new HostShellBootstrap { Document = document, FullPath = dashhostPath };
        ApplyHostShell(bootstrap, hostShell);

        HostSettingsOverlay.Apply(bootstrap);

        GitCatalogSynchronizer.PrepareDeferredSync(bootstrap);

        return (bootstrap, hostShell);
    }

    public static CatalogBootstrap LoadCatalog(DashSpecTomlRoot bootstrap, string contentRoot)
    {
        DashSpecParser.EnsureModuleParsersRegistered();
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

    private static void ApplyHostShell(DashSpecTomlRoot bootstrap, HostShellBootstrap hostShell)
    {
        var host = hostShell.Document;
        var hostDirectory = Path.GetDirectoryName(hostShell.FullPath)!;
        bootstrap.Dashboard.CatalogPath = ResolveCatalogPath(hostDirectory, host.CatalogPath);

        if (host.Configuration.TryGetValue("language", out var language)
            && !string.IsNullOrWhiteSpace(language))
        {
            bootstrap.Presentation.Language = language.Trim();
        }

        if (host.Configuration.TryGetValue("display_timezone", out var timeZone)
            && !string.IsNullOrWhiteSpace(timeZone))
        {
            bootstrap.Presentation.DisplayTimeZone = timeZone.Trim();
        }

        if (host.Presentation.TryGetValue("color_scheme", out var colorScheme)
            && !string.IsNullOrWhiteSpace(colorScheme))
        {
            bootstrap.Presentation.ColorScheme = colorScheme.Trim();
        }

        if (host.Presentation.TryGetValue("large_field_filter_layout", out var filterLayout)
            && !string.IsNullOrWhiteSpace(filterLayout))
        {
            bootstrap.Presentation.LargeFieldFilterLayout = filterLayout.Trim();
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
        HostBootstrapPlanetTomlGuard.RejectLegacyRuntimeLinks(root, configPath);

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

    public static string? ResolveDashhostPath(string contentRoot, string? dashhostReference) =>
        ResolveDashhostFile(contentRoot, dashhostReference);

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
