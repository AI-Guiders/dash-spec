using DashSpec.Host.Configuration;
using DashSpec.Host.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DashSpec.Host.Tests;

internal static class HostTestServices
{
    public static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDashSpecHostInfrastructure();
        return services.BuildServiceProvider();
    }

    public static IHostBootstrap HostBootstrap => CreateProvider().GetRequiredService<IHostBootstrap>();

    public static IHostDatabaseInitializer HostDatabase => CreateProvider().GetRequiredService<IHostDatabaseInitializer>();

    public static IHostPathResolver PathResolver => CreateProvider().GetRequiredService<IHostPathResolver>();
}
