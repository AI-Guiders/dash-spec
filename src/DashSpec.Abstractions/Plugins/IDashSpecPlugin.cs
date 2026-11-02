using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Abstractions.Plugins;

/// <summary>DashSpec plugin entry (connectors, diagram families, extension blocks, interactions).</summary>
public interface IDashSpecPlugin
{
    string Id { get; }

    string DisplayName { get; }

    PluginTier Tier { get; }

    /// <summary>Presentation properties this plugin understands (e.g. <c>lane_height</c>):
    /// the plugin-declared vocabulary for <c>presentation</c>/<c>chrome</c> blocks — the
    /// same "plugins feed the parser" channel as click sources (ADR-0107) and extension
    /// keywords. Unknown-to-everyone properties stay loud parse errors.</summary>
    IEnumerable<string> PresentationProperties => [];

    void ConfigureServices(IServiceCollection services, IConfiguration configuration);

    void RegisterContributors(IDashSpecContributorRegistry registry);
}
