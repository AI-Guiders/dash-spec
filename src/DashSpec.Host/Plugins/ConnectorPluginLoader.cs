using System.Reflection;
using System.Runtime.Loader;
using DashSpec.Abstractions.Connectors;
using DashSpec.Host.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashSpec.Host.Plugins;

public static class ConnectorPluginLoader
{
    public static ConnectorPluginManifest LoadManifest(DashSpecTomlRoot toml) =>
        new()
        {
            DefaultConnectorId = string.IsNullOrWhiteSpace(toml.Plugins.DefaultConnectorId)
                ? "sqlserver"
                : toml.Plugins.DefaultConnectorId,
            Plugins = toml.Plugins.Load
                .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Assembly))
                .Select(x => new ConnectorPluginEntry { Id = x.Id, Assembly = x.Assembly })
                .ToList(),
        };

    public static void RegisterPlugins(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        ConnectorPluginManifest manifest,
        ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;
        var connectorsDir = Path.Combine(AppContext.BaseDirectory, "connectors");
        if (!Directory.Exists(connectorsDir))
        {
            connectorsDir = Path.Combine(environment.ContentRootPath, "connectors");
        }

        var loaded = 0;

        foreach (var entry in manifest.Plugins)
        {
            var assemblyPath = Path.IsPathRooted(entry.Assembly)
                ? entry.Assembly
                : Path.Combine(connectorsDir, entry.Assembly);

            if (!File.Exists(assemblyPath))
            {
                logger.LogWarning("Connector plugin assembly not found: {AssemblyPath}", assemblyPath);
                continue;
            }

            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
            var pluginTypes = assembly.GetTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IConnectorPlugin).IsAssignableFrom(t))
                .ToList();

            foreach (var pluginType in pluginTypes)
            {
                if (Activator.CreateInstance(pluginType) is not IConnectorPlugin plugin)
                {
                    continue;
                }

                if (!string.Equals(plugin.Id, entry.Id, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning(
                        "Skipping plugin {PluginType} (id {PluginId}, expected {ExpectedId})",
                        pluginType.FullName,
                        plugin.Id,
                        entry.Id);
                    continue;
                }

                plugin.ConfigureServices(services, configuration);
                loaded++;
                logger.LogInformation("Loaded connector plugin {PluginId} from {Assembly}", plugin.Id, assemblyPath);
            }
        }

        if (loaded == 0)
        {
            throw new InvalidOperationException(
                $"No connector plugins loaded. Check dash-spec.toml [[plugins.load]] and folder {connectorsDir}.");
        }

        services.AddSingleton(manifest);
        services.AddSingleton<ConnectorRegistry>();
    }
}
