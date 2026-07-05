using DashSpec.Abstractions.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Host.Plugins.Builtins;

public sealed class CardViewsBuiltinPlugin : IDashSpecPlugin
{
    public string Id => "card_views";

    public string DisplayName => "Card diagram view switch";

    public PluginTier Tier => PluginTier.Core;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IDashSpecActionHandler, SwitchViewActionHandler>();
    }

    public void RegisterContributors(IDashSpecContributorRegistry registry)
    {
        registry.AddExtensionBlock(new ExtensionBlockContributorDescriptor(
            Id,
            "views",
            ["Card"],
            ["label", "diagram", "default", "widget"]));

        registry.AddCardChrome(new CardChromeContributorDescriptor(
            Id,
            "views",
            CardChromeRenderKind.ViewSwitch));

        registry.AddActionHandler(new ActionHandlerDescriptor(
            Id,
            "switch_view",
            "Switch card diagram view"));
    }
}

internal sealed class SwitchViewActionHandler(ICardViewState viewState) : IDashSpecActionHandler
{
    public string ActionId => "switch_view";

    public ValueTask<DashSpecActionOutcome> ExecuteAsync(
        DashSpecActionContext context,
        IReadOnlyDictionary<string, string> args,
        CancellationToken cancellationToken = default)
    {
        if (!args.TryGetValue("view", out var viewId) || string.IsNullOrWhiteSpace(viewId))
        {
            return ValueTask.FromResult(new DashSpecActionOutcome());
        }

        viewState.SetActiveView(context.CardId, viewId);
        return ValueTask.FromResult(new DashSpecActionOutcome(
            Kind: DashSpecActionOutcomeKind.RefreshCard,
            RefreshCardId: context.CardId));
    }
}
