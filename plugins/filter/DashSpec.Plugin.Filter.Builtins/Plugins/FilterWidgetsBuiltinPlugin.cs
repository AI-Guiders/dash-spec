using DashSpec.Abstractions.Plugins;
using DashSpec.Plugin.Filter.Builtins.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Plugin.Filter.Builtins.Plugins;

public sealed class FilterWidgetsBuiltinPlugin : IDashSpecPlugin
{
    public string Id => "filter_widgets_builtin";

    public string DisplayName => "Built-in filter widgets";

    public PluginTier Tier => PluginTier.Core;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void RegisterContributors(IDashSpecContributorRegistry registry)
    {
        Register(registry, "combobox", typeof(ComboboxFilterWidget), ["field"]);
        Register(registry, "select", typeof(SelectFilterWidget), ["field"]);
        Register(registry, "chips", typeof(ChipsFilterWidget), ["field"]);
        Register(registry, "range", typeof(DateFilterWidget), ["date"]);
        Register(registry, "day", typeof(DateFilterWidget), ["date"]);
        Register(registry, "top", typeof(TopFilterWidget), ["top"]);
    }

    private static void Register(
        IDashSpecContributorRegistry registry,
        string widgetId,
        Type componentType,
        IReadOnlyList<string> kinds)
    {
        registry.AddFilterWidget(new FilterWidgetContributorDescriptor("filter_widgets_builtin", widgetId, kinds));
        registry.RegisterFilterWidgetComponent(widgetId, componentType);
    }
}
