using DashSpec.Core.Parsing;

namespace DashSpec.Host.Configuration;

/// <summary>Host bootstrap: dash-spec.toml → spec → обязательный @runtime TOML.</summary>
public static class DashSpecBootstrap
{
    public static DashSpecTomlRoot LoadBootstrap(IHostEnvironment environment)
    {
        var contentRoot = environment.ContentRootPath;
        var bootstrapPath = Path.Combine(contentRoot, "dash-spec.toml");
        if (!File.Exists(bootstrapPath))
        {
            throw new InvalidOperationException(
                "Host requires dash-spec.toml with [dashboard] spec_path pointing to a .dashspec file.");
        }

        var bootstrap = DashSpecTomlLoader.LoadFile(bootstrapPath);
        bootstrap = OverlayOptionalToml(bootstrap, Path.Combine(contentRoot, "dash-spec.dev.toml"));
        bootstrap = OverlayOptionalToml(bootstrap, Path.Combine(contentRoot, "dash-spec.local.toml"));

        var envSpecPath = Environment.GetEnvironmentVariable("DASHSPEC_SPEC_PATH");
        if (!string.IsNullOrWhiteSpace(envSpecPath))
        {
            bootstrap.Dashboard.SpecPath = envSpecPath;
        }

        if (string.IsNullOrWhiteSpace(bootstrap.Dashboard.SpecPath))
        {
            throw new InvalidOperationException(
                "dash-spec.toml: set [dashboard] spec_path to your .dashspec file.");
        }

        return bootstrap;
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
        var specPath = ResolveSpecPath(environment.ContentRootPath, bootstrap.Dashboard.SpecPath);
        if (!File.Exists(specPath))
        {
            throw new FileNotFoundException("DashSpec file not found.", specPath);
        }

        var specText = File.ReadAllText(specPath);
        var configPath = ResolveRuntimeConfigPath(specPath, specText);
        var runtime = DashSpecTomlLoader.LoadFile(configPath);
        runtime.Dashboard.SpecPath = bootstrap.Dashboard.SpecPath;
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
                В .dashspec нет @runtime — укажите в начале файла, например:
                  @runtime "demo.toml"
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
}
