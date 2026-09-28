using DashSpec.Abstractions.Plugins;
using DashSpec.Abstractions.Viz;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Plugin.Viz.Builtins.Plugins;

public abstract class VizDashSpecPluginBase : IDashSpecPlugin
{
    public abstract string Id { get; }

    public abstract string DisplayName { get; }

    public PluginTier Tier => PluginTier.Core;

    protected abstract string RendererId { get; }

    protected abstract string RendererDisplayName { get; }

    protected abstract Type CardVizComponentType { get; }

    protected virtual Type? CardToolbarComponentType => null;

    protected abstract IVizPlugin CreateBackend();

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddSingleton(CreateBackend());

    public void RegisterContributors(IDashSpecContributorRegistry registry)
    {
        registry.AddVizRenderer(new VizRendererDescriptor(Id, RendererId, RendererDisplayName));
        registry.RegisterCardVizComponent(RendererId, CardVizComponentType);
        if (CardToolbarComponentType is not null)
        {
            registry.RegisterVizCardToolbar(RendererId, CardToolbarComponentType);
        }
    }
}
