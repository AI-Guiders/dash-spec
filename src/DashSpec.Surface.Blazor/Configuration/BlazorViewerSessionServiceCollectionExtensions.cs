using System.Globalization;
using DashSpec.Abstractions.Viz;
using DashSpec.Surface.Blazor.Commands;
using DashSpec.Surface.Blazor.Commands.Constructors;
using DashSpec.Surface.Blazor.Services;
using DashSpec.Surface.Blazor.Services.Localization;
using DashSpec.Surface.Blazor.Services.Presentation;
using DashSpec.Surface.Blazor.Services.Diagnostics;
using DashSpec.Surface.Blazor.Services.Rendering;
using DashSpec.Viz.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Surface.Blazor.Configuration;

/// <summary>Viewer session + UI services (ADR-0099 B3.3). Host registers platform adapters separately.</summary>
public static class BlazorViewerSessionServiceCollectionExtensions
{
    public static IServiceCollection AddDashSpecBlazorViewerSession(
        this IServiceCollection services,
        CultureInfo uiCulture,
        TimeZoneInfo displayTimeZone)
    {
        services.AddSingleton<LoadTrace>();
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
        services.AddScoped<IVizCardToolbarLocalizer, VizCardToolbarLocalizer>();
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
