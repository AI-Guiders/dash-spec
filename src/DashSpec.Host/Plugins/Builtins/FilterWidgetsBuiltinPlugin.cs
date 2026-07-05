using DashSpec.Abstractions.Plugins;
using DashSpec.Core.Model;
using DashSpec.Host.Components.Filters;

namespace DashSpec.Host.Plugins.Builtins;

public sealed class FilterWidgetsBuiltinPlugin : IDashSpecPlugin
{
    public string Id => "filter_widgets_builtin";

    public string DisplayName => "Built-in filter widgets";

    public PluginTier Tier => PluginTier.Core;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IFilterWidgetRenderer, ComboboxFilterWidgetRenderer>();
        services.AddSingleton<IFilterWidgetRenderer, SelectFilterWidgetRenderer>();
        services.AddSingleton<IFilterWidgetRenderer, ChipsFilterWidgetRenderer>();
        services.AddSingleton<IFilterWidgetRenderer, DateRangeFilterWidgetRenderer>();
        services.AddSingleton<IFilterWidgetRenderer, DateDayFilterWidgetRenderer>();
        services.AddSingleton<IFilterWidgetRenderer, TopFilterWidgetRenderer>();
        services.AddSingleton<FilterWidgetRegistry>();
    }

    public void RegisterContributors(IDashSpecContributorRegistry registry)
    {
        registry.AddFilterWidget(new FilterWidgetContributorDescriptor(Id, "combobox", ["field"]));
        registry.AddFilterWidget(new FilterWidgetContributorDescriptor(Id, "select", ["field"]));
        registry.AddFilterWidget(new FilterWidgetContributorDescriptor(Id, "chips", ["field"]));
        registry.AddFilterWidget(new FilterWidgetContributorDescriptor(Id, "range", ["date"]));
        registry.AddFilterWidget(new FilterWidgetContributorDescriptor(Id, "day", ["date"]));
        registry.AddFilterWidget(new FilterWidgetContributorDescriptor(Id, "top", ["top"]));
    }
}

internal sealed class ComboboxFilterWidgetRenderer : IFilterWidgetRenderer
{
    public string WidgetId => "combobox";

    public bool CanRender(FilterDefinition filter) => filter.Kind is FilterKind.Field;

    public Type ComponentType => typeof(FieldMultiSelect);
}

internal sealed class SelectFilterWidgetRenderer : IFilterWidgetRenderer
{
    public string WidgetId => "select";

    public bool CanRender(FilterDefinition filter) =>
        filter.Kind is FilterKind.Field && filter.IsSingleSelectField;

    public Type ComponentType => typeof(FieldMultiSelect);
}

internal sealed class ChipsFilterWidgetRenderer : IFilterWidgetRenderer
{
    public string WidgetId => "chips";

    public bool CanRender(FilterDefinition filter) => filter.Kind is FilterKind.Field;

    public Type ComponentType => typeof(FieldChipsSelect);
}

internal sealed class DateRangeFilterWidgetRenderer : IFilterWidgetRenderer
{
    public string WidgetId => "range";

    public bool CanRender(FilterDefinition filter) =>
        filter.Kind is FilterKind.Date && !filter.IsDayWidget;

    public Type ComponentType => typeof(GrainAwareDateInput);
}

internal sealed class DateDayFilterWidgetRenderer : IFilterWidgetRenderer
{
    public string WidgetId => "day";

    public bool CanRender(FilterDefinition filter) =>
        filter.Kind is FilterKind.Date && filter.IsDayWidget;

    public Type ComponentType => typeof(GrainAwareDateInput);
}

internal sealed class TopFilterWidgetRenderer : IFilterWidgetRenderer
{
    public string WidgetId => "top";

    public bool CanRender(FilterDefinition filter) => filter.Kind is FilterKind.Top;

    public Type ComponentType => typeof(TopFilterInput);
}
