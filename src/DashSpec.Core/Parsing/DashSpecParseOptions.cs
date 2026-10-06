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
        LinkOnlyReferencedDiagramUnits = false,
        ExtensionBlockKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "views" },
    };

    public bool MergeReferencedTabModules { get; init; } = true;

    /// <summary>Editor/LSP: skip <c>!include</c> paths ending in <c>/</c> or <c>\</c> (in-progress completion).</summary>
    public bool TolerateIncompleteIncludes { get; init; }

    /// <summary>Legacy: process each include line in source order. Default: union glob membership then link (ADR-0089).</summary>
    public ModuleLinkMode ModuleLinkMode { get; init; } = ModuleLinkMode.MembershipUnion;

    /// <summary>Parse only report-referenced .dashdiagram units from glob membership (runtime default).</summary>
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
