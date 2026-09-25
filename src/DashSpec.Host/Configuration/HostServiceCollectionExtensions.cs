using DashSpec.Host.Services;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Git;
using DashSpec.Host.Services.Settings;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DashSpec.Host.Configuration;

public static class HostServiceCollectionExtensions
{
    public static IServiceCollection AddDashSpecHostInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton<IDashSpecTomlLoader, DashSpecTomlLoader>();
        services.TryAddSingleton<IHostPathResolver, HostPathResolver>();
        services.TryAddSingleton<IHostSettingsOverlay, HostSettingsOverlayService>();
        services.TryAddSingleton<IGitCatalogSynchronizer, GitCatalogSynchronizer>();
        services.TryAddSingleton<IHostDatabaseInitializer, HostDatabaseInitializer>();
        services.TryAddSingleton<IHostBootstrap, HostBootstrapService>();
        return services;
    }
}
