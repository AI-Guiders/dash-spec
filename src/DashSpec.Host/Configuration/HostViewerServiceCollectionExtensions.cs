using DashSpec.Core.Platform;
using DashSpec.Execution.Runtime.Platform;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Connectors;
using DashSpec.Host.Services.Loading;
using DashSpec.Host.Services.Platform;

namespace DashSpec.Host.Configuration;

/// <summary>Host adapters for platform report bootstrap (ADR-0099 B3 wiring).</summary>
public static class HostViewerServiceCollectionExtensions
{
    public static IServiceCollection AddDashSpecHostViewerPlatform(this IServiceCollection services)
    {
        services.AddSingleton<IReportRuntimePaths, HostReportRuntimePaths>();
        services.AddSingleton<IReportConnectorResolver, HostReportConnectorResolver>();
        services.AddSingleton<IReportParseOptionsSource, HostReportParseOptionsSource>();
        services.AddSingleton<IReportBootstrapEnvironment, HostReportBootstrapEnvironment>();
        services.AddSingleton<IReportFieldOptionsCache, HostReportFieldOptionsCache>();
        services.AddSingleton<IFieldOptionsCache, FieldOptionsCache>();
        services.AddSingleton<RuntimeConnectorResolver>();
        services.AddScoped<IDashboardSpecLoader, DashboardSpecLoader>();
        return services;
    }
}
