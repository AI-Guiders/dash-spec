using DashSpec.Abstractions.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Plugin.ViewSwitch;

public sealed class ViewSwitchDashSpecPlugin : IDashSpecPlugin
{
    public string Id => "card_views";

    public string DisplayName => "Card diagram view switch";

    public PluginTier Tier => PluginTier.Extended;

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
