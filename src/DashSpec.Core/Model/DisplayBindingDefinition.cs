namespace DashSpec.Core.Model;

/// <summary>Page/report display slot bindings for title templates (ADR-0058).</summary>
public sealed record DisplayBindingDefinition(
    IReadOnlyDictionary<string, string> Slots)
{
    public static DisplayBindingDefinition Empty { get; } =
        new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
}
