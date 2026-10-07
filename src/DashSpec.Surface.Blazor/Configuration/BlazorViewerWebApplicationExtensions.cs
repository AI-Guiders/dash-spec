using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DashSpec.Surface.Blazor.Configuration;

/// <summary>Blazor viewer HTTP foundation (ADR-0099 B3). Planet Host maps endpoints after this.</summary>
public static class BlazorViewerWebApplicationExtensions
{
    public static WebApplication UseDashSpecBlazorViewerFoundation(this WebApplication app)
    {
        app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);
        app.UseAntiforgery();
        app.UseStaticFiles();
        app.MapStaticAssets();
        return app;
    }

    public static RazorComponentsEndpointConventionBuilder MapDashSpecBlazorViewer<TApp>(
        this WebApplication app,
        params Assembly[] additionalAssemblies)
        where TApp : IComponent
    {
        var builder = app.MapRazorComponents<TApp>()
            .AddInteractiveServerRenderMode();
        foreach (var assembly in additionalAssemblies)
        {
            builder.AddAdditionalAssemblies(assembly);
        }

        return builder;
    }
}
