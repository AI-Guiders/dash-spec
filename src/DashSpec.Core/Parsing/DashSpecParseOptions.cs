using DashSpec.Abstractions.Plugins;

namespace DashSpec.Core.Parsing;

public sealed class DashSpecParseOptions
{
    public static DashSpecParseOptions Default { get; } = new();

    /// <summary>Editor/LSP: per-file validation, builtin extension blocks, no tab dashspec merge.</summary>
    public static DashSpecParseOptions Editor { get; } = new()
    {
        MergeReferencedTabModules = false,
        TolerateIncompleteIncludes = true,
        ExtensionBlockKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "views" },
    };

    public bool MergeReferencedTabModules { get; init; } = true;

    /// <summary>Editor/LSP: skip <c>!include</c> paths ending in <c>/</c> or <c>\</c> (in-progress completion).</summary>
    public bool TolerateIncompleteIncludes { get; init; }

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
