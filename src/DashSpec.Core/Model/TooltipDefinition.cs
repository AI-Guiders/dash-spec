namespace DashSpec.Core.Model;

/// <summary>Tooltip content entity (ADR-0029): named slots + interpolated string. No display chrome.</summary>
public sealed record TooltipDefinition(
    string Id,
    IReadOnlyDictionary<string, string> Variables,
    string Template);
