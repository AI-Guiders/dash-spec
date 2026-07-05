using DashSpec.Abstractions.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyProduct.DashSpec;

public sealed class MyProductDashSpecPlugin : IDashSpecPlugin
{
    public string Id => "my_product";

    public string DisplayName => "My product DashSpec extensions";

    public PluginTier Tier => PluginTier.Product;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void RegisterContributors(IDashSpecContributorRegistry registry)
    {
        registry.AddExtensionBlock(new ExtensionBlockContributorDescriptor(
            Id,
            "buttons",
            ["Card"],
            ["label", "action"]));
    }
}
