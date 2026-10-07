using DashSpec.Core.Platform;
using DashSpec.Execution.Bootstrap;
using DashSpec.Execution.Compilation;
using DashSpec.Execution.Runtime.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DashSpec.Execution.Configuration;

/// <summary>Headless report platform DI (ADR-0099 B2/B4).</summary>
public static class ReportPlatformServiceCollectionExtensions
{
    public static IServiceCollection AddDashSpecReportPlatform(this IServiceCollection services)
    {
        services.TryAddSingleton<IReportCompiler, ReportCompiler>();
        services.TryAddScoped<ReportFieldOptionsLoader>();
        services.TryAddScoped<IReportSpecBootstrap, ReportSpecBootstrap>();
        return services;
    }
}
