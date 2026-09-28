namespace DashSpec.Host.Plugins;

public sealed class FilterWidgetComponentRegistry
{
    private readonly Dictionary<string, Type> _components = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string widgetId, Type componentType)
    {
        if (string.IsNullOrWhiteSpace(widgetId))
        {
            throw new ArgumentException("Widget id is required.", nameof(widgetId));
        }

        if (!typeof(Microsoft.AspNetCore.Components.IComponent).IsAssignableFrom(componentType))
        {
            throw new ArgumentException(
                $"Type {componentType.FullName} must implement IComponent.",
                nameof(componentType));
        }

        if (!_components.TryAdd(widgetId, componentType))
        {
            throw new InvalidOperationException($"Duplicate filter widget component registration for '{widgetId}'.");
        }
    }

    public Type? TryGet(string widgetId) =>
        _components.TryGetValue(widgetId, out var componentType) ? componentType : null;
}
