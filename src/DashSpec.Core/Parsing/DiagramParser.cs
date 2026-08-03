using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class DiagramParser
{
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
            return new DiagramDefinition(name, properties);
        }

        var overrides = !reader.IsOnNewline() && !reader.IsEof
            ? PropertyBlockParser.Parse(
                reader,
                DiagramKindRegistry.AllBindingProperties(),
                $"diagram {name}",
                allowExtensionProperties: true)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return new DiagramDefinition(string.Empty, overrides, name);
    }
}
