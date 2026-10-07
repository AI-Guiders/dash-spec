using DashSpec.Core.Platform;
using DashSpec.Execution.Compilation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DashSpec.Surface.Blazor.Configuration;

/// <summary>Platform viewer composition (ADR-0099 B3). ASP.NET wiring remains in Host entry.</summary>
public static class ViewerSurfaceServiceCollectionExtensions
{
    public static IServiceCollection AddDashSpecViewerPlatform(this IServiceCollection services)
    {
        services.TryAddSingleton<IReportCompiler, ReportCompiler>();
        return services;
    }
}
