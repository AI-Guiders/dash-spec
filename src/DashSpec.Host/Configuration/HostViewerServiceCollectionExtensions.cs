using System.Globalization;
using DashSpec.Abstractions.Hosting;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Connectors;
using DashSpec.Host.Services.Loading;
using DashSpec.Host.Services.Platform;
using DashSpec.Abstractions.Viewer;
using DashSpec.Surface.Blazor.Configuration;
using DashSpec.Surface.Blazor.Services.Presentation;
using DashSpec.Execution.Runtime.Platform;
using DashSpec.Core.Platform;
using DashSpec.Core.Runtime;

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

    public static IServiceCollection AddDashSpecHostViewerSession(
        this IServiceCollection services,
        CultureInfo uiCulture,
        TimeZoneInfo displayTimeZone) =>
        services.AddDashSpecBlazorViewerSession(uiCulture, displayTimeZone);

    /// <summary>Host → Surface viewer port adapters (ADR-0099 B3).</summary>
    public static IServiceCollection AddDashSpecHostViewerPorts(this IServiceCollection services)
    {
        services.AddSingleton<HostPresentationSignals>();
        services.AddSingleton<ICatalogUsageClient, CatalogUsageClientAdapter>();
        services.AddSingleton<IViewerExternalLinksProvider, ViewerExternalLinksProviderAdapter>();
        services.AddSingleton<IViewerShellChrome, ViewerShellChromeAdapter>();
        services.AddSingleton<IViewerPresentationOptions, ViewerPresentationOptionsAdapter>();
        services.AddSingleton<ILayoutSlotRendererRegistry, LayoutSlotRendererRegistry>();
        return services;
    }
}
