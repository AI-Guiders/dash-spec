using DashSpec.Viz;

namespace DashSpec.Viewer.Plugins;

public sealed class CardVizComponentRegistry : ICardVizComponentResolver
{
    private readonly Dictionary<string, Type> _components = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string pluginId, Type componentType)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            throw new ArgumentException("Plugin id is required.", nameof(pluginId));
        }

        if (!typeof(Microsoft.AspNetCore.Components.IComponent).IsAssignableFrom(componentType))
        {
            throw new ArgumentException(
                $"Type {componentType.FullName} must implement IComponent.",
                nameof(componentType));
        }

        if (!_components.TryAdd(pluginId, componentType))
        {
            throw new InvalidOperationException($"Duplicate viz component registration for '{pluginId}'.");
        }
    }

    public Type? TryGet(string pluginId) =>
        TryGetComponentType(pluginId);

    public Type? TryGetComponentType(string renderPluginId) =>
        _components.TryGetValue(renderPluginId, out var componentType) ? componentType : null;
}
