using DashSpec.Core.Model;

namespace DashSpec.Core.Runtime;

public static class DiagramBindings
{
    public static string Column(DiagramDefinition diagram, string key)
    {
        if (!diagram.Properties.TryGetValue(key, out var column) || string.IsNullOrWhiteSpace(column))
        {
            throw new InvalidOperationException($"Diagram requires '{key}' property.");
        }

        return column;
    }

    public static string? Label(DiagramDefinition diagram, string key) =>
        diagram.Properties.TryGetValue(LabelKey(key), out var label) && !string.IsNullOrWhiteSpace(label)
            ? label
            : null;

    public static string LabelKey(string key) => $"{key}_as";
}
