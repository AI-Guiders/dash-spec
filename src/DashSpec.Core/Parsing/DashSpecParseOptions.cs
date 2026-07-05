using DashSpec.Abstractions.Plugins;

namespace DashSpec.Core.Parsing;

public sealed class DashSpecParseOptions
{
    public static DashSpecParseOptions Default { get; } = new();

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
