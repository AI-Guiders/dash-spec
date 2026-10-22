using DashSpec.Execution.Runtime;
using DashSpec.Execution.Compilation;
using DashSpec.Execution.Parsing;
using DashSpec.Abstractions.Hosting;
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
            HostBootstrapTomlGuard.RejectLegacyToml(bootstrap, bootstrapPath);
        }

        bootstrap = OverlayOptionalToml(bootstrap, Path.Combine(contentRoot, "dash-spec.dev.toml"));
        if (!ShouldSkipLocalHostOverlay(environment))
        {
            bootstrap = OverlayOptionalToml(bootstrap, Path.Combine(contentRoot, "dash-spec.local.toml"));
        }

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
        RegisterGridDefault(hostShell);

        settingsOverlay.Apply(bootstrap);
        gitCatalogSynchronizer.PrepareDeferredSync(bootstrap);

        return (bootstrap, hostShell);
    }

    public CatalogBootstrap LoadCatalog(DashSpecTomlRoot bootstrap, string contentRoot)
    {
        DashSpecParser.EnsureModuleParsersRegistered();
        var catalogPath = pathResolver.ResolveCatalogPath(contentRoot, bootstrap.Dashboard.CatalogPath);
        return new CatalogBootstrap(CatalogReader.ParseFile(catalogPath), catalogPath);
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
        // The visual layer ([ADR-0111] visual pilot) is a host decision: the host toml [viz]
        // pick rides the runtime config — corpus runtime files never carry it.
        runtime.Viz = bootstrap.Viz;
        ValidateRuntimeConfig(runtime, configPath);
        return runtime;
    }

    /// <summary>
    /// Register the host default grid ([ADR-0111] §2): the embedded <c>host.ds</c> grid drawing
    /// serves every document that draws no grid of its own — documents draw, the host defaults, code stays silent.
    /// </summary>
    private static void RegisterGridDefault(HostShellBootstrap hostShell) =>
        DashSpec.Modeling.Parse.Layout.GridDefaultBridge.Default =
            Microsoft.FSharp.Core.FSharpOption<DashSpec.Modeling.Parse.Layout.LayoutDefinition>.Some(
                hostShell.DefaultGrid());

    private void ApplyHostShell(DashSpecTomlRoot bootstrap, HostShellBootstrap hostShell)
    {
        var host = hostShell.Document;
        var hostDirectory = Path.GetDirectoryName(hostShell.FullPath)!;
        var tomlCatalog = bootstrap.Dashboard.CatalogPath;
        var dashhostCatalog = string.IsNullOrWhiteSpace(host.CatalogPath)
            ? null
            : pathResolver.ResolveCatalogPath(hostDirectory, host.CatalogPath);
        bootstrap.Dashboard.CatalogPath = HostOpsResolution.ResolveCatalogPath(
            tomlCatalog,
            dashhostCatalog,
            tomlCatalog);

        if (host.Configuration.TryGetValue("language", out var language)
            && !string.IsNullOrWhiteSpace(language))
        {
            bootstrap.Presentation.Language = DashSpecCultures.Parse(language.Trim()).Name;
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

    private static bool ShouldSkipLocalHostOverlay(IHostEnvironment environment) =>
        string.Equals(environment.EnvironmentName, "E2E", StringComparison.OrdinalIgnoreCase)
        || string.Equals(
            Environment.GetEnvironmentVariable("DASHSPEC_SKIP_LOCAL_CONFIG"),
            "1",
            StringComparison.Ordinal);

    private DashSpecTomlRoot OverlayOptionalToml(DashSpecTomlRoot root, string path)
    {
        if (!File.Exists(path))
        {
            return root;
        }

        var overlay = tomlLoader.LoadFile(path);
        HostBootstrapTomlGuard.RejectLegacyToml(overlay, path);
        return tomlLoader.Merge(root, overlay);
    }

    private static void ValidateRuntimeConfig(DashSpecTomlRoot root, string configPath)
    {
        HostBootstrapTomlGuard.RejectLegacyRuntimeLinks(root, configPath);

        if (root.Plugins.Load.Count == 0)
        {
            throw new InvalidOperationException(
                $"Config '{configPath}' must define [[plugins.load]] entries.");
        }

        if (root.Providers.Values.All(section => string.IsNullOrWhiteSpace(section.ConnectionString)))
        {
            throw new InvalidOperationException(
                $"Config '{configPath}' must define at least one [providers.*] connection_string.");
        }
    }
}
