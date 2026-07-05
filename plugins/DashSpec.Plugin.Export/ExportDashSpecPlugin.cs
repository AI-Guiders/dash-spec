using DashSpec.Abstractions.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Plugin.Export;

public sealed class ExportDashSpecPlugin : IDashSpecPlugin
{
    public string Id => "card_export";

    public string DisplayName => "Card export actions";

    public PluginTier Tier => PluginTier.Extended;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDashSpecActionHandler, CsvExportActionHandler>();
    }

    public void RegisterContributors(IDashSpecContributorRegistry registry)
    {
        registry.AddExtensionBlock(new ExtensionBlockContributorDescriptor(
            Id,
            "buttons",
            ["Card"],
            ["label", "action", "on"]));

        registry.AddActionHandler(new ActionHandlerDescriptor(
            Id,
            "csv_export",
            "Export card data as CSV"));

        registry.AddCardChrome(new CardChromeContributorDescriptor(
            Id,
            "buttons",
            CardChromeRenderKind.Buttons));

        registry.AddPhraseTemplate(new PhraseTemplateDescriptor(
            Id,
            "csv_export",
            PhraseScopes.OnClick,
            "export card as {format} with delimiter {delimiter}",
            [
                new PhraseSlotDescriptor("format", PhraseSlotKind.Ident),
                new PhraseSlotDescriptor("delimiter", PhraseSlotKind.String),
            ]));
    }
}
