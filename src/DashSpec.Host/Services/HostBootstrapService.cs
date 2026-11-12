using DashSpec.Execution.Runtime;
using DashSpec.Execution.Compilation;
using DashSpec.Execution.Parsing;
using DashSpec.Abstractions.Hosting;
using DashSpec.Composition.Shell;
using DashSpec.Host.Configuration;
using DashSpec.Stack.Radzen;
using DashSpec.Host.Services.Abstractions;
using DashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;

namespace DashSpec.Host.Services;

/// <summary>Host bootstrap: ops TOML → <c>.dashhost</c> → catalog → default entry @runtime TOML.</summary>
public sealed class HostBootstrapService(
    IDashSpecTomlLoader tomlLoader,
    IHostPathResolver pathResolver,
    IHostSettingsOverlay settingsOverlay,
    IGitCatalogSynchronizer gitCatalogSynchronizer,
    Microsoft.Extensions.Logging.ILogger<HostBootstrapService> logger) : IHostBootstrap
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
            var envHost = Environment.GetEnvironmentVariable("DASHSPEC_HOST");
            if (!string.IsNullOrWhiteSpace(envHost))
            {
                bootstrap.Host.Dashhost = envHost.Trim();
            }
            else
            {
                bootstrap.Host.Dashhost = TryAutoDiscoverHostSpec(contentRoot) ?? string.Empty;
            }
        }

        if (string.IsNullOrWhiteSpace(bootstrap.Host.Dashhost))
        {
            throw new InvalidOperationException(
                "[host] dashhost is required (declare a 'module host <id>' in the workspace or set DASHSPEC_HOST).");
        }

        var dashhostPath = pathResolver.ResolveDashhostPath(contentRoot, bootstrap.Host.Dashhost)
            ?? throw new FileNotFoundException(
                $"Host dashhost not found: '{bootstrap.Host.Dashhost}' (content root: {contentRoot}).");

        DashSpecParser.EnsureModuleParsersRegistered();
        var document = HostModuleParser.ParseFile(dashhostPath);
        var hostShell = new HostShellBootstrap { Document = document, FullPath = dashhostPath };
        ApplyHostShell(bootstrap, hostShell);
        DashSpecDefaultShell.RegisterGridDefault();

        settingsOverlay.Apply(bootstrap);
        WarnUnknownColorScheme(bootstrap);
        gitCatalogSynchronizer.PrepareDeferredSync(bootstrap);

        return (bootstrap, hostShell);
    }

    public CatalogBootstrap LoadCatalog(DashSpecTomlRoot bootstrap, string contentRoot)
    {
        DashSpecParser.EnsureModuleParsersRegistered();
        var catalogPath = pathResolver.ResolveCatalogPath(contentRoot, bootstrap.Dashboard.CatalogPath);
        // ADR-0124: compiled row types ride next to the catalog (dsc build types). Absent
        // assembly = the wire mode; present = the renderer gets honest CLR TItems.
        // ADR-0124: the compiled row types ride next to the catalog — one convention,
        // no upward searching.
        var catalogDir = Path.GetDirectoryName(catalogPath) ?? contentRoot;
        var typedRowsPath = Path.Combine(catalogDir, "DashSpec.TypedRows.dll");
        if (File.Exists(typedRowsPath))
        {
            DashSpec.Execution.Runtime.CompiledRowTypes.Load(typedRowsPath);
        }
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
        ValidateRuntimeConfig(runtime, configPath);
        return runtime;
    }

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

        // ADR-0112 §6: the `host` block is the settings home; the legacy configuration/
        // presentation blocks keep serving un-migrated host modules — host block wins per key.
        string? Pick(string key, IReadOnlyDictionary<string, string> legacy)
        {
            if (host.Host.TryGetValue(key, out var fromHost) && !string.IsNullOrWhiteSpace(fromHost))
            {
                return fromHost.Trim();
            }

            return legacy.TryGetValue(key, out var fromLegacy) && !string.IsNullOrWhiteSpace(fromLegacy)
                ? fromLegacy.Trim()
                : null;
        }

        if (Pick("language", host.Configuration) is { } language)
        {
            bootstrap.Presentation.Language = DashSpecCultures.Parse(language).Name;
        }

        if (Pick("display_timezone", host.Configuration) is { } timeZone)
        {
            bootstrap.Presentation.DisplayTimeZone = timeZone;
        }

        if (Pick("color_scheme", host.Presentation) is { } colorScheme)
        {
            bootstrap.Presentation.ColorScheme = colorScheme;
        }

        if (Pick("large_field_filter_layout", host.Presentation) is { } filterLayout)
        {
            bootstrap.Presentation.LargeFieldFilterLayout = filterLayout;
        }

        if (Pick("render", host.Presentation) is { } renderStack)
        {
            bootstrap.Presentation.RenderStack = renderStack;
        }

        if (Pick("provider_default", host.Presentation) is { } providerDefault)
        {
            bootstrap.Plugins.DefaultProviderId = providerDefault;
        }
    }

    /// <summary>ADR-0112 §2: an unknown theme word is the stack default + a loud
    /// warning — never a silent wrong theme (the old "High contrast" admin option
    /// rendered plain dark). The dictionary belongs to the Radzen stack; selfmade
    /// does not read the token.</summary>
    private void WarnUnknownColorScheme(DashSpecTomlRoot bootstrap)
    {
        var stack = (bootstrap.Presentation.RenderStack ?? string.Empty).Trim();
        if (!string.Equals(stack, "radzen", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var scheme = (bootstrap.Presentation.ColorScheme ?? string.Empty).Trim();
        if (RadzenColorSchemes.Find(scheme) is not null)
        {
            return;
        }

        logger.LogWarning(
            "color_scheme '{Scheme}' is not in the Radzen stack theme dictionary ({Words}) — rendering '{Default}' instead (ADR-0112 §2).",
            scheme,
            string.Join(", ", RadzenColorSchemes.All.Select(t => t.Token)),
            RadzenColorSchemes.DefaultToken);
    }

    private static string? TryAutoDiscoverHostSpec(string contentRoot)
    {
        var searchDirs = new[]
        {
            Path.Combine(contentRoot, "dashspec"),
            contentRoot,
            Path.GetFullPath(Path.Combine(contentRoot, "..", "..", "samples", "ai-landscape")),
            Path.GetFullPath(Path.Combine(contentRoot, "..", "..", "samples", "demo")),
        };

        foreach (var dir in searchDirs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(dir))
            {
                continue;
            }

            var idx = DashSpec.Modeling.Parse.Document.UnitIndexModule.build(dir);
            var hostEntry = idx.Entries.FirstOrDefault(e =>
                e.Kind is not null &&
                string.Equals(e.Kind.Value, "host", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(e.Path));

            if (hostEntry is not null)
            {
                return Path.GetFullPath(hostEntry.Path);
            }
        }

        return null;
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
