using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Surface.Blazor.Configuration;

/// <summary>Blazor viewer shell (ADR-0099 B3). Planet Host calls this + host-specific services.</summary>
public static class BlazorViewerServiceCollectionExtensions
{
    public static IServiceCollection AddDashSpecBlazorViewerShell(this IServiceCollection services)
    {
        services.AddDashSpecViewerPlatform();
        services.AddRazorComponents()
            .AddInteractiveServerComponents();
        return services;
    }
}
