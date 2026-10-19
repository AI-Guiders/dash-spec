using DashSpec.Abstractions.Plugins;


namespace DashSpec.Compiler.Contract;

public sealed class DashSpecParseOptions
{
    public static DashSpecParseOptions Default { get; } = new();

    /// <summary>Editor/LSP: per-file validation, builtin extension blocks, no tab dashspec merge.</summary>
    public static DashSpecParseOptions Editor { get; } = new()
    {
        MergeReferencedTabModules = false,
        ExtensionBlockKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "views" },
    };

    public bool MergeReferencedTabModules { get; init; } = true;

    /// <summary>Block keywords registered by plugin extensions (e.g. <c>views</c>).</summary>
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
