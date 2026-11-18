#nullable enable

using DashSpec.Abstractions.Plugins;
using DashSpec.Host.Endpoints;
using DashSpec.Plugin.Commands.Components;
using DashSpec.Surface.Blazor.Commands;
using DashSpec.Surface.Blazor.Commands.Constructors;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Plugin.Commands;

/// <summary>
/// Vertical slice plugin for the DashSpec Command Plane (CCL bar, Ctrl+K palette, target highlights, and HTTP endpoints).
/// Isolates all <c>AIGuiders.Platform.*CommandPlane*</c> and GDL catalog emit dependencies outside <c>DashSpec.Surface.Blazor</c> and <c>DashSpec.Host</c>.
/// </summary>
public sealed class CommandsDashSpecPlugin : IDashSpecPlugin, IDashSpecEndpointContributor
{
    public const string PluginId = "command_bar";

    public string Id => PluginId;

    public string DisplayName => "DashSpec Command Bar & Palette";

    public PluginTier Tier => PluginTier.Extended;

    public Type? ScriptsComponent => typeof(CommandsScripts);

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<DashSpecCommandPluginRegistry>();
        services.AddScoped<DashboardSlashConstructorHost>();
        services.AddScoped<DashboardCommandSession>();
        services.AddScoped<ICommandTargetHighlighter>(sp => sp.GetRequiredService<DashboardCommandSession>());
        services.AddScoped<DashboardCommandExecutor>();
        services.AddScoped<DashboardFilterCommandService>();
    }

    public void RegisterContributors(IDashSpecContributorRegistry registry)
    {
        registry.RegisterPluginComponent(
            DashSpecPluginComponents.HostCommandBar,
            typeof(HostCommandBar));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.MapDashboardCommandEndpoints();
}
