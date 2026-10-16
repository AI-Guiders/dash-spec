namespace DashSpec.Core.Model;

public sealed record ModuleExtensionsDefinition(
    IReadOnlyList<string> EnabledPluginIds,
    IReadOnlyList<ModuleExtensionImport> Imports)
{
    public static ModuleExtensionsDefinition Empty { get; } =
        new([], []);
}

public sealed record ModuleExtensionImport(string PluginId, string? AssemblyPath);
