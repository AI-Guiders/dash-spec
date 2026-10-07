using DashSpec.Host.Components;
using DashSpec.Host.Endpoints;
using DashSpec.Host.Middleware;
using DashSpec.Plugin.Filter.Builtins;
using DashSpec.Plugin.Viz.Builtins.Plugins;
using DashSpec.Surface.Blazor;
using DashSpec.Surface.Blazor.Configuration;

namespace DashSpec.Host.Configuration;

public static class HostPlanetWebApplicationExtensions
{
    public static WebApplication UseDashSpecPlanetHost(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
        }

        var urlsEnv = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? string.Empty;
        if (urlsEnv.Contains("https://", StringComparison.OrdinalIgnoreCase))
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseMiddleware<DashSpecAccessMiddleware>();
        app.UseMiddleware<DashSpecClientIdMiddleware>();
        app.UseDashSpecBlazorViewerFoundation();

        app.MapAccessEndpoints();
        app.MapCatalogSyncEndpoints();
        app.MapPluginEndpoints();
        app.MapDashboardCommandEndpoints();

        app.MapDashSpecBlazorViewer<App>(
            typeof(SurfaceAssemblyMarker).Assembly,
            typeof(VizBuiltinsPluginCatalog).Assembly,
            typeof(FilterBuiltinsPluginCatalog).Assembly);

        app.MapDevEndpoints();

        return app;
    }
}
