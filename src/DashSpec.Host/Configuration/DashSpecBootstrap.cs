namespace DashSpec.Host.Configuration;

/// <summary>Host bootstrap: dash-spec.toml → spec → обязательный @config TOML.</summary>
public static class DashSpecBootstrap
{
    public static DashSpecTomlRoot LoadBootstrap(IHostEnvironment environment)
    {
        var bootstrapPath = Path.Combine(environment.ContentRootPath, "dash-spec.toml");
        if (!File.Exists(bootstrapPath))
        {
            throw new InvalidOperationException(
                "Host requires dash-spec.toml with [dashboard] spec_path pointing to a .dashspec file.");
        }

        var bootstrap = DashSpecTomlLoader.LoadFile(bootstrapPath);
        if (string.IsNullOrWhiteSpace(bootstrap.Dashboard.SpecPath))
        {
            throw new InvalidOperationException(
                "dash-spec.toml: set [dashboard] spec_path to your .dashspec file.");
        }

        return bootstrap;
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

        var searchDirs = new List<string> { Path.GetDirectoryName(specFullPath)! };
        if (!string.IsNullOrWhiteSpace(libraryFallbackDirectory))
        {
            searchDirs.Add(libraryFallbackDirectory);
        }

        foreach (var dir in searchDirs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var libraryPath = Path.GetFullPath(Path.Combine(dir, libraryRelative));
            if (File.Exists(libraryPath))
            {
                return libraryPath;
            }
        }

        var primary = Path.GetFullPath(Path.Combine(searchDirs[0], libraryRelative));
        throw new FileNotFoundException(
            $"DashSpec @diagramlibrary not found: '{libraryRelative}' (resolved: {primary}).",
            primary);
    }

    public static string ResolveRuntimeConfigPath(
        string specFullPath,
        string specText,
        string? configFallbackDirectory = null)
    {
        var configRelative = DashSpec.Core.Parsing.DashSpecParser.ReadConfigPath(specText);
        if (string.IsNullOrWhiteSpace(configRelative))
        {
            throw new InvalidOperationException(
                """
                В .dashspec нет @config — укажите в начале файла, например:
                  @config "demo.toml"
                Файл конфига должен содержать [connectors.*] и [plugins].
                """);
        }

        var searchDirs = new List<string> { Path.GetDirectoryName(specFullPath)! };
        if (!string.IsNullOrWhiteSpace(configFallbackDirectory))
        {
            searchDirs.Add(configFallbackDirectory);
        }

        foreach (var dir in searchDirs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var configPath = Path.GetFullPath(Path.Combine(dir, configRelative));
            if (File.Exists(configPath))
            {
                return configPath;
            }
        }

        var primary = Path.GetFullPath(Path.Combine(searchDirs[0], configRelative));
        throw new FileNotFoundException(
            $"DashSpec @config not found: '{configRelative}' (resolved: {primary}).",
            primary);
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
        Path.IsPathRooted(specPath)
            ? specPath
            : Path.GetFullPath(Path.Combine(contentRoot, specPath));
}
