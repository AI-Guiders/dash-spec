using DashSpec.Host.Components;
using DashSpec.Host.Endpoints;
using DashSpec.Host.Middleware;
using DashSpec.Surface.Blazor;
using DashSpec.Surface.Blazor.Configuration;

namespace DashSpec.Host.Configuration;

public static class DashSpecHostWebApplicationExtensions
{
    public static WebApplication UseDashSpecHost(this WebApplication app)
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

        var contributorRegistry = app.Services.GetRequiredService<DashSpec.Composition.Plugins.DashSpecContributorRegistry>();
        var componentAssemblies = new HashSet<System.Reflection.Assembly>
        {
            typeof(SurfaceAssemblyMarker).Assembly,
        };
        foreach (var type in contributorRegistry.VizComponents.Values)
        {
            componentAssemblies.Add(type.Assembly);
        }

        foreach (var type in contributorRegistry.FilterWidgetComponents.Values)
        {
            componentAssemblies.Add(type.Assembly);
        }

        foreach (var type in contributorRegistry.CardChromeComponents.Values)
        {
            componentAssemblies.Add(type.Assembly);
        }

        foreach (var type in contributorRegistry.PluginComponents.Values)
        {
            componentAssemblies.Add(type.Assembly);
        }

        foreach (var type in contributorRegistry.HeadComponents)
        {
            componentAssemblies.Add(type.Assembly);
        }

        foreach (var type in contributorRegistry.ScriptsComponents)
        {
            componentAssemblies.Add(type.Assembly);
        }

        app.MapDashSpecBlazorViewer<App>(componentAssemblies.ToArray());

        app.MapDevEndpoints();

        return app;
    }
}
