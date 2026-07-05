using DashSpec.Abstractions.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Host.Plugins.Builtins;

public sealed class OnClickDefaultPlugin : IDashSpecPlugin
{
    public string Id => "on_click_default";

    public string DisplayName => "Default card click interactions";

    public PluginTier Tier => PluginTier.Core;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<OnClickInteractionService>();
    }

    public void RegisterContributors(IDashSpecContributorRegistry registry)
    {
        registry.AddInteractionHandler(new InteractionHandlerDescriptor(
            Id,
            "selection_list",
            "Selection list from tooltip"));

        registry.AddInteractionHandler(new InteractionHandlerDescriptor(
            Id,
            "drill_down",
            "Apply filter context and navigate (set/goto wiring)"));

        registry.AddPhraseTemplate(new PhraseTemplateDescriptor(
            Id,
            "drill_down",
            PhraseScopes.OnClick,
            "drill to {tab} with {target} from {from}",
            [
                new PhraseSlotDescriptor("tab", PhraseSlotKind.Ident),
                new PhraseSlotDescriptor("target", PhraseSlotKind.Ident),
                new PhraseSlotDescriptor("from", PhraseSlotKind.Ident),
            ]));
    }
}
