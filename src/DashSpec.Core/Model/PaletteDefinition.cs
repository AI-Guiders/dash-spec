namespace DashSpec.Core.Model;

/// <summary>Palette unit resolved by the parser: the id from <c>module palette &lt;id&gt;</c> and its color table.
/// The document carries the resolved palette, not a file path (ADR-0098).</summary>
public sealed record PaletteDefinition(
    string Id,
    IReadOnlyDictionary<string, string> Properties);
