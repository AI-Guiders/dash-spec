using DashSpec.Filters;
using DashSpec.Modeling.Parse.Filter;

namespace DashSpec.Composition.Plugins;

public sealed class FilterWidgetRegistry : IFilterWidgetComponentResolver
{
    private readonly IReadOnlyDictionary<string, Type> _components;
    private readonly DashSpecContributorRegistry _contributors;

    public FilterWidgetRegistry(DashSpecContributorRegistry contributors)
    {
        // Live map (ADR-0117): the kernel gathers widget component types; the surface owns their registry class.
        _components = contributors.FilterWidgetComponents;
        _contributors = contributors;
    }

    public Type ResolveComponentType(FilterDefinition filter)
    {
        var widgetId = ResolveWidgetId(filter);
        _components.TryGetValue(widgetId, out var componentType);

        if (componentType is not null)
        {
            return componentType;
        }

        return ResolveFallbackComponent(filter);
    }

    public bool IsKnownWidget(string? widgetId) =>
        string.IsNullOrWhiteSpace(widgetId) ||
        _contributors.FilterWidgets.ContainsKey(widgetId);

    private static string ResolveWidgetId(FilterDefinition filter)
    {
        // ADR-0118: the expression density decides first (inline -> chips, collapsed ->
        // combobox/select); legacy `widget =` words follow; kind fallbacks close.
        if (filter.Kind is FilterKind.Field)
        {
            if (filter.IsInlineDensity)
            {
                return "chips";
            }

            if (string.Equals(filter.Density?.Value, "collapsed", StringComparison.OrdinalIgnoreCase))
            {
                return filter.IsSingleSelectField ? "select" : "combobox";
            }
        }

        return filter.Widget?.Value?.ToLowerInvariant() switch
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
    }

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

        return (_components.TryGetValue(fallbackId, out var fallback) ? fallback : null)
            ?? throw new InvalidOperationException($"No filter widget component registered for '{fallbackId}'.");
    }
}
