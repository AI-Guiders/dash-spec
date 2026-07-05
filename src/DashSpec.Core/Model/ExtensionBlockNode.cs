namespace DashSpec.Core.Model;

/// <summary>Generic extension block IR (ADR-0032/0033).</summary>
public sealed record ExtensionBlockNode(
    string Keyword,
    IReadOnlyDictionary<string, string> Properties,
    IReadOnlyList<ExtensionBlockNode> Nested);

public sealed record ModuleExtensionsDefinition(
    IReadOnlyList<string> EnabledPluginIds,
    IReadOnlyList<ModuleExtensionImport> Imports)
{
    public static ModuleExtensionsDefinition Empty { get; } =
        new([], []);
}

public sealed record ModuleExtensionImport(string PluginId, string? AssemblyPath);
