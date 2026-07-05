using DashSpec.Abstractions.Plugins;
using DashSpec.Host.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Host.Endpoints;

internal static class PluginEndpointMapper
{
    public static void MapPluginEndpoints(this WebApplication app)
    {
        var registry = app.Services.GetRequiredService<DashSpecContributorRegistry>();
        foreach (var contributor in registry.EndpointContributors)
        {
            contributor.MapEndpoints(app);
        }
    }
}
