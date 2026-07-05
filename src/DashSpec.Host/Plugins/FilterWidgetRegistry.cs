using DashSpec.Abstractions.Plugins;
using DashSpec.Core.Model;
using DashSpec.Host.Components.Filters;

namespace DashSpec.Host.Plugins;

public interface IFilterWidgetRenderer
{
    string WidgetId { get; }

    bool CanRender(FilterDefinition filter);

    Type ComponentType { get; }
}

public sealed class FilterWidgetRegistry
{
    private readonly IReadOnlyDictionary<string, IFilterWidgetRenderer> _byId;
    private readonly DashSpecContributorRegistry _contributors;

    public FilterWidgetRegistry(
        IEnumerable<IFilterWidgetRenderer> renderers,
        DashSpecContributorRegistry contributors)
    {
        _byId = renderers.ToDictionary(x => x.WidgetId, StringComparer.OrdinalIgnoreCase);
        _contributors = contributors;
    }

    public IFilterWidgetRenderer Resolve(FilterDefinition filter)
    {
        var widgetId = ResolveWidgetId(filter);
        if (_byId.TryGetValue(widgetId, out var renderer) && renderer.CanRender(filter))
        {
            return renderer;
        }

        return ResolveFallback(filter);
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

    private IFilterWidgetRenderer ResolveFallback(FilterDefinition filter)
    {
        var fallbackId = filter.Kind switch
        {
            FilterKind.Date => "range",
            FilterKind.Top => "top",
            FilterKind.Field when filter.IsSingleSelectField => "select",
            FilterKind.Field => "combobox",
            _ => "combobox",
        };

        return _byId[fallbackId];
    }
}
