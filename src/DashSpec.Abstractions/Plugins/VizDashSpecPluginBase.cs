using DashSpec.Abstractions.Viz;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Abstractions.Plugins;

/// <summary>
/// Viz plugin contract base (ADR-0117): kernel ABI without a UI stack — registers the
/// renderer descriptor, card viz component and optional toolbar. Card chrome (the diagram
/// host inside a card) is surface-owned, never a plugin concern: plugins are vertical.
/// </summary>
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

    /// <summary>This renderer's presentation knobs (see IDashSpecPlugin.PresentationProperties).</summary>
    public virtual IEnumerable<string> PresentationProperties => [];

    public virtual void ConfigureServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddSingleton(CreateBackend());

    public virtual void RegisterContributors(IDashSpecContributorRegistry registry)
    {
        registry.AddVizRenderer(new VizRendererDescriptor(Id, RendererId, RendererDisplayName));
        registry.RegisterCardVizComponent(RendererId, CardVizComponentType);
        if (CardToolbarComponentType is not null)
        {
            registry.RegisterVizCardToolbar(RendererId, CardToolbarComponentType);
        }
    }
}
