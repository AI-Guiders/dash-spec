using DashSpec.Abstractions.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Host.Plugins.Builtins;

public sealed class ScopeBuiltinPlugin : IDashSpecPlugin
{
    public string Id => "scope_builtin";

    public string DisplayName => "Built-in document scopes";

    public PluginTier Tier => PluginTier.Core;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void RegisterContributors(IDashSpecContributorRegistry registry)
    {
        registry.AddScopeContributor(new ScopeContributorDescriptor(
            Id,
            "card",
            "card",
            null,
            ["bind", "diagram", "datasource", "on click", "place", "presentation"]));

        registry.AddScopeContributor(new ScopeContributorDescriptor(
            Id,
            PhraseScopes.OnClick,
            "on click",
            "card",
            ["show", "set", "goto", "invoke", "run"]));

        registry.AddScopeContributor(new ScopeContributorDescriptor(
            Id,
            PhraseScopes.ExtensionBlock,
            "extension",
            "card",
            []));
    }
}
