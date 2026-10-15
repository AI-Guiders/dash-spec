using DashSpec.Abstractions.Plugins;

namespace DashSpec.Core.Parsing;

public sealed class DashSpecParseOptions
{
    public static DashSpecParseOptions Default { get; } = new();

    /// <summary>Editor/LSP: per-file validation, builtin extension blocks, no tab dashspec merge.</summary>
    public static DashSpecParseOptions Editor { get; } = new()
    {
        MergeReferencedTabModules = false,
        LinkOnlyReferencedDiagramUnits = false,
        ExtensionBlockKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "views" },
    };

    public bool MergeReferencedTabModules { get; init; } = true;

    /// <summary>Parse only units referenced by the report (runtime default).</summary>
    public bool LinkOnlyReferencedDiagramUnits { get; init; } = true;

    public IReadOnlySet<string> ExtensionBlockKeywords { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> ExtensionBlockPluginIds { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<PhraseTemplateDescriptor> PhraseTemplates { get; init; } = [];

    public IReadOnlySet<string> KnownActionHandlers { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlySet<string> KnownInteractionHandlers { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
