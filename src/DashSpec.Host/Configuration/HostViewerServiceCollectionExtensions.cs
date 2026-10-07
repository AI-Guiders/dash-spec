using System.Globalization;
using DashSpec.Abstractions.Viz;
using DashSpec.Core.Platform;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime.Platform;
using DashSpec.Host.Commands;
using DashSpec.Host.Commands.Constructors;
using DashSpec.Host.Services;
using DashSpec.Abstractions.Hosting;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Connectors;
using DashSpec.Host.Services.Loading;
using DashSpec.Host.Services.Localization;
using DashSpec.Host.Services.Platform;
using DashSpec.Host.Services.Presentation;
using DashSpec.Host.Services.Rendering;

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
        TimeZoneInfo displayTimeZone)
    {
        services.AddScoped<ICardCellDrillState, CardCellDrillState>();
        services.AddScoped<ICardFoldState, CardFoldState>();
        services.AddSingleton<ReportFormatDefaultsAmbient>();
        services.AddScoped<ICardRenderer, CardRenderService>();
        services.AddScoped<IDashboardSession, DashboardSessionService>();
        services.AddScoped<DashboardFilterUiState>();
        services.AddScoped<CardLocalFilterUiStore>();
        services.AddScoped<IDashboardCultureAmbient>(_ =>
            new DashboardCultureAmbient(uiCulture, displayTimeZone));
        services.AddScoped<DashboardLocalizer>();
        services.AddScoped<DashboardSlashConstructorHost>();
        services.AddScoped<DashboardCommandSession>();
        services.AddScoped<DashboardRefreshCoordinator>();
        services.AddScoped<DashboardFilterCommandService>();
        services.AddScoped<DashboardCommandExecutor>();
        services.AddScoped<DashboardHostCommandCoordinator>();
        services.AddScoped<DashboardPageController>();
        return services;
    }
}
