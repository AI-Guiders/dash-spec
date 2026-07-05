using DashSpec.Core.Parsing;

namespace DashSpec.Host.Plugins;

public sealed class DashSpecParseOptionsProvider(DashSpecContributorRegistry registry)
{
    public DashSpecParseOptions CreateOptions() =>
        new()
        {
            ExtensionBlockKeywords = registry.ExtensionBlockKeywords,
            ExtensionBlockPluginIds = registry.ExtensionBlocks.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.PluginId,
                StringComparer.OrdinalIgnoreCase),
            PhraseTemplates = registry.PhraseTemplates,
            KnownActionHandlers = registry.ActionHandlers.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase),
            KnownInteractionHandlers = registry.InteractionHandlers.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase),
        };
}
