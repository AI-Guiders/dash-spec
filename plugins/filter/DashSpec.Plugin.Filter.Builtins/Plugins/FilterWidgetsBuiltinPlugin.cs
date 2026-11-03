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
        // ADR-0118: stack-registered overrides (IFilterDateInputOverride) replace this
        // default when the stack ships its own date controls (last registration wins).
        services.AddSingleton<Components.IFilterDateInputOverride>(
            new Components.DefaultFilterDateInputOverride());
    }

    public void RegisterContributors(IDashSpecContributorRegistry registry)
    {
        // ADR-0118 role contracts: selector over values/dates/numbers; day/range carry
        // the flatpickr skin until the Radzen stack binding replaces them there.
        Register(registry, "combobox", typeof(ComboboxFilterWidget), ["field"], "values", "cascade");
        Register(registry, "select", typeof(SelectFilterWidget), ["field"], "values", "cascade");
        Register(registry, "chips", typeof(ChipsFilterWidget), ["field"], "values", "cascade");
        Register(registry, "range", typeof(DateRangeFilterWidget), ["date"], "dates", "own");
        Register(registry, "day", typeof(DateFilterWidget), ["date"], "dates", "own");
        Register(registry, "top", typeof(TopFilterWidget), ["top"], "numbers", "cascade");
    }

    private static void Register(
        IDashSpecContributorRegistry registry,
        string widgetId,
        Type componentType,
        IReadOnlyList<string> kinds,
        string domain = "values",
        string skin = "cascade")
    {
        registry.AddFilterWidget(new FilterWidgetContributorDescriptor("filter_widgets_builtin", widgetId, kinds, "selector", domain, skin));
        registry.RegisterFilterWidgetComponent(widgetId, componentType);
    }
}
