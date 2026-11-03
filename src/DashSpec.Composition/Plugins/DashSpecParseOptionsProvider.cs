using DashSpec.Execution.Runtime;

using DashSpec.Compiler.Contract;

namespace DashSpec.Composition.Plugins;

public sealed class DashSpecParseOptionsProvider(DashSpecContributorRegistry registry)
{
    public DashSpecParseOptions CreateOptions()
    {
        var options = new DashSpecParseOptions
        {
            ExtensionBlockKeywords = registry.ExtensionBlockKeywords,
            ExtensionBlockPluginIds = registry.ExtensionBlocks.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.PluginId,
                StringComparer.OrdinalIgnoreCase),
            PhraseTemplates = registry.PhraseTemplates,
            KnownActionHandlers = registry.ActionHandlers.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase),
            KnownInteractionHandlers = registry.InteractionHandlers.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase),
            KnownWidgetRoles = registry.FilterWidgets.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase),
            KnownClickSources = registry.ClickSources.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase),
            KnownPresentationProperties = registry.PresentationProperties.ToHashSet(StringComparer.OrdinalIgnoreCase),
        };

        // The F# grammar reads the presentation vocabulary through a bridge (the same
        // pattern as the parse bridges): plugin knobs become parseable process-wide
        // once a kernel with those plugins has built its options.
        if (options.KnownPresentationProperties.Count > 0)
        {
            DashSpec.Modeling.Parse.PropertySchemas.registerPresentationProperties(options.KnownPresentationProperties);
        }

        return options;
    }
}
