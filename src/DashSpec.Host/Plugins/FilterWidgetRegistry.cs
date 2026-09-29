using DashSpec.Core.Model;
using DashSpec.Filters;

namespace DashSpec.Host.Plugins;

public sealed class FilterWidgetRegistry : IFilterWidgetComponentResolver
{
    private readonly FilterWidgetComponentRegistry _components;
    private readonly DashSpecContributorRegistry _contributors;

    public FilterWidgetRegistry(
        FilterWidgetComponentRegistry components,
        DashSpecContributorRegistry contributors)
    {
        _components = components;
        _contributors = contributors;
    }

    public Type ResolveComponentType(FilterDefinition filter)
    {
        var widgetId = ResolveWidgetId(filter);
        var componentType = _components.TryGet(widgetId);
        if (componentType is not null)
        {
            return componentType;
        }

        return ResolveFallbackComponent(filter);
    }

    public bool IsKnownWidget(string? widgetId) =>
        string.IsNullOrWhiteSpace(widgetId) ||
        _contributors.FilterWidgets.ContainsKey(widgetId);

    private static string ResolveWidgetId(FilterDefinition filter) =>
        filter.Widget?.ToLowerInvariant() switch
        {
            "select" => "select",
            "chips" => "chips",
            "combobox" => "combobox",
            "day" => "day",
            "range" => "range",
            _ when filter.Kind is FilterKind.Top => "top",
            _ when filter.Kind is FilterKind.Date => "range",
            _ when filter.IsSingleSelectField => "select",
            _ => "combobox",
        };

    private Type ResolveFallbackComponent(FilterDefinition filter)
    {
        var fallbackId = filter.Kind switch
        {
            FilterKind.Date => "range",
            FilterKind.Top => "top",
            FilterKind.Field when filter.IsSingleSelectField => "select",
            FilterKind.Field => "combobox",
            _ => "combobox",
        };

        return _components.TryGet(fallbackId)
            ?? throw new InvalidOperationException($"No filter widget component registered for '{fallbackId}'.");
    }
}
