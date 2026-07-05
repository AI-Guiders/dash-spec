using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Abstractions.Plugins;

/// <summary>DashSpec plugin entry (connectors, diagram families, extension blocks, interactions).</summary>
public interface IDashSpecPlugin
{
    string Id { get; }

    string DisplayName { get; }

    PluginTier Tier { get; }

    void ConfigureServices(IServiceCollection services, IConfiguration configuration);

    void RegisterContributors(IDashSpecContributorRegistry registry);
}
