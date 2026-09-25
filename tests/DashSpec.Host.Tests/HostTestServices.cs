using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Host.Tests;

internal static class HostTestServices
{
    public static IHostDatabaseInitializer CreateHostDatabase()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostDatabaseInitializer, HostDatabaseInitializer>();
        return services.BuildServiceProvider().GetRequiredService<IHostDatabaseInitializer>();
    }
}
