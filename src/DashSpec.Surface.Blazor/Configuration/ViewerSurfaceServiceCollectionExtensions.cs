using DashSpec.Execution.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Surface.Blazor.Configuration;

/// <summary>Platform viewer composition (ADR-0099 B3). ASP.NET wiring remains in Host entry.</summary>
public static class ViewerSurfaceServiceCollectionExtensions
{
    /// <summary>Registers headless compile + bootstrap ports (host must register platform adapters).</summary>
    public static IServiceCollection AddDashSpecViewerPlatform(this IServiceCollection services) =>
        services.AddDashSpecReportPlatform();
}
