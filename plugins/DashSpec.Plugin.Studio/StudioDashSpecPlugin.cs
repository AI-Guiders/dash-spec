#nullable enable

using DashSpec.Abstractions.Plugins;
using DashSpec.Plugin.Studio.Components;
using DashSpec.Plugin.Studio.Endpoints;
using DashSpec.Plugin.Studio.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Plugin.Studio;

/// <summary>
/// Vertical slice plugin for DashSpec Studio (<c>id = "studio"</c>, ADR-0133):
/// provides the <c>/studio</c> authoring workspace with isolated per-author Draft Overlays,
/// <c>DashSpec.CodeCenter</c> AST/ConceptGraph inspection, Presence badges, SHA-256 optimistic concurrency,
/// and atomic publish + Git commit integration.
/// </summary>
public sealed class StudioDashSpecPlugin : IDashSpecPlugin, IDashSpecEndpointContributor
{
    public const string PluginId = "studio";

    public string Id => PluginId;

    public string DisplayName => "DashSpec Studio (Authoring, CodeCenter & Git)";

    public PluginTier Tier => PluginTier.Extended;

    public Type? ScriptsComponent => typeof(StudioScripts);

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<StudioPresenceTracker>();
        services.AddSingleton<StudioGitService>();
        services.AddSingleton<StudioWorkspaceService>();
    }

    public void RegisterContributors(IDashSpecContributorRegistry registry)
    {
        registry.RegisterPluginComponent(
            DashSpecPluginComponents.HostStudioLink,
            typeof(HostStudioLink));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.MapDashSpecStudioEndpoints();
}
