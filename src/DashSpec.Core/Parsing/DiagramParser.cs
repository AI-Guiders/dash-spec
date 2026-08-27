using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class DiagramParser
{
    private static readonly HashSet<string> BannedTooltipPropertyKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "tooltip",
        "tooltip_time",
        "tooltip_format",
        "tooltip_split",
        "tooltip_as",
    };

    public static DiagramDefinition Parse(TokenReader reader)
    {
        var name = reader.ReadIdent();
        return ParseAfterKindIdent(reader, name);
    }

    public static DiagramDefinition ParseAfterKindIdent(TokenReader reader, string name)
    {
        if (DiagramKindRegistry.TryResolve(name, out var spec))
        {
            var properties = PropertyBlockParser.Parse(
                reader,
                DiagramKindRegistry.GetProperties(name),
                $"diagram {name}",
                spec.AllowExtensionProperties);
            RejectLegacyTooltipProperties(properties, name);
            return new DiagramDefinition(name, properties);
        }

        var overrides = !reader.IsOnNewline() && !reader.IsEof
            ? PropertyBlockParser.Parse(
                reader,
                DiagramKindRegistry.AllBindingProperties(),
                $"diagram {name}",
                allowExtensionProperties: true)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        RejectLegacyTooltipProperties(overrides, name);
        return new DiagramDefinition(string.Empty, overrides, name);
    }

    private static void RejectLegacyTooltipProperties(
        IReadOnlyDictionary<string, string> properties,
        string context)
    {
        foreach (var key in properties.Keys)
        {
            if (BannedTooltipPropertyKeys.Contains(key))
            {
                throw new DashSpecParseException(
                    $"Diagram '{context}': property '{key}' was removed (ADR-0029). " +
                    "Use @tooltip entity and inspect block instead.");
            }
        }
    }
}
