using DashSpec.Core.Parsing;
using DashSpec.Host.Configuration;
using DashSpec.Host.Services.Abstractions;
using DashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;

namespace DashSpec.Host.Services;

/// <summary>Host bootstrap: ops TOML → <c>.dashhost</c> → catalog → default entry @runtime TOML.</summary>
public sealed class HostBootstrapService(
    IDashSpecTomlLoader tomlLoader,
    IHostPathResolver pathResolver,
    IHostSettingsOverlay settingsOverlay,
    IGitCatalogSynchronizer gitCatalogSynchronizer) : IHostBootstrap
{
    public DashSpecTomlRoot LoadBootstrap(IHostEnvironment environment) =>
        LoadBootstrapWithHost(environment).Bootstrap;

    public (DashSpecTomlRoot Bootstrap, HostShellBootstrap HostShell) LoadBootstrapWithHost(
        IHostEnvironment environment)
    {
        var contentRoot = environment.ContentRootPath;
        var bootstrapPath = Path.Combine(contentRoot, "dash-spec.toml");

        var bootstrap = File.Exists(bootstrapPath)
            ? tomlLoader.LoadFile(bootstrapPath)
            : new DashSpecTomlRoot();
        if (File.Exists(bootstrapPath))
        {
            HostBootstrapPlanetTomlGuard.RejectLegacyPlanetToml(bootstrap, bootstrapPath);
        }

        bootstrap = OverlayOptionalToml(bootstrap, Path.Combine(contentRoot, "dash-spec.dev.toml"));
        bootstrap = OverlayOptionalToml(bootstrap, Path.Combine(contentRoot, "dash-spec.local.toml"));

        if (string.IsNullOrWhiteSpace(bootstrap.Host.Dashhost))
        {
            throw new InvalidOperationException(
                "[host] dashhost is required in dash-spec.toml or dash-spec.local.toml.");
        }

        var dashhostPath = pathResolver.ResolveDashhostPath(contentRoot, bootstrap.Host.Dashhost)
            ?? throw new FileNotFoundException(
                $"Host dashhost not found: '{bootstrap.Host.Dashhost}' (content root: {contentRoot}).");

        DashSpecParser.EnsureModuleParsersRegistered();
        var document = HostModuleParser.ParseFile(dashhostPath);
        var hostShell = new HostShellBootstrap { Document = document, FullPath = dashhostPath };
        ApplyHostShell(bootstrap, hostShell);

        settingsOverlay.Apply(bootstrap);
        gitCatalogSynchronizer.PrepareDeferredSync(bootstrap);

        return (bootstrap, hostShell);
    }

    public CatalogBootstrap LoadCatalog(DashSpecTomlRoot bootstrap, string contentRoot)
    {
        DashSpecParser.EnsureModuleParsersRegistered();
        var catalogPath = pathResolver.ResolveCatalogPath(contentRoot, bootstrap.Dashboard.CatalogPath);
        return new CatalogBootstrap(CatalogParser.ParseFile(catalogPath), catalogPath);
    }

    public string ResolveActiveSpecFullPath(CatalogBootstrap catalog, string? catalogEntryId = null)
    {
        var entryId = string.IsNullOrWhiteSpace(catalogEntryId)
            ? catalog.Document.DefaultEntryId
            : catalogEntryId;
        return catalog.ResolveEntrySpecFullPath(entryId);
    }

    public DashSpecAccessOptions LoadAccessOptions(IHostEnvironment environment)
    {
        var bootstrap = LoadBootstrap(environment);
        return new DashSpecAccessOptions { ApiKey = bootstrap.Access.ApiKey };
    }

    public DashSpecTomlRoot Load(IHostEnvironment environment)
    {
        var bootstrap = LoadBootstrap(environment);
        var catalog = LoadCatalog(bootstrap, environment.ContentRootPath);
        var specPath = ResolveActiveSpecFullPath(catalog);
        if (!File.Exists(specPath))
        {
            throw new FileNotFoundException("DashSpec file not found.", specPath);
        }

        var specText = File.ReadAllText(specPath);
        var configPath = pathResolver.ResolveRuntimeConfigPath(specPath, specText);
        var runtime = tomlLoader.LoadFile(configPath);
        runtime.Dashboard.CatalogPath = bootstrap.Dashboard.CatalogPath;
        ValidateRuntimeConfig(runtime, configPath);
        return runtime;
    }

    private void ApplyHostShell(DashSpecTomlRoot bootstrap, HostShellBootstrap hostShell)
    {
        var host = hostShell.Document;
        var hostDirectory = Path.GetDirectoryName(hostShell.FullPath)!;
        bootstrap.Dashboard.CatalogPath = pathResolver.ResolveCatalogPath(hostDirectory, host.CatalogPath);

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

    private DashSpecTomlRoot OverlayOptionalToml(DashSpecTomlRoot root, string path)
    {
        if (!File.Exists(path))
        {
            return root;
        }

        var overlay = tomlLoader.LoadFile(path);
        HostBootstrapPlanetTomlGuard.RejectLegacyPlanetToml(overlay, path);
        return tomlLoader.Merge(root, overlay);
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
}
