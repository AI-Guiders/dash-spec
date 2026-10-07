namespace DashSpec.Viewer.Plugins;

public sealed class VizCardToolbarRegistry
{
    private readonly Dictionary<string, Type> _toolbars = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string rendererId, Type componentType)
    {
        if (string.IsNullOrWhiteSpace(rendererId))
        {
            throw new ArgumentException("Renderer id is required.", nameof(rendererId));
        }

        if (!typeof(Microsoft.AspNetCore.Components.IComponent).IsAssignableFrom(componentType))
        {
            throw new ArgumentException(
                $"Type {componentType.FullName} must implement IComponent.",
                nameof(componentType));
        }

        if (!_toolbars.TryAdd(rendererId, componentType))
        {
            throw new InvalidOperationException($"Duplicate viz toolbar registration for '{rendererId}'.");
        }
    }

    public Type? TryGet(string rendererId) =>
        _toolbars.TryGetValue(rendererId, out var componentType) ? componentType : null;
}
