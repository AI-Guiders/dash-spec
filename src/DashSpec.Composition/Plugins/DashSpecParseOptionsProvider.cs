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
            KnownClickSources = registry.ClickSources.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase),
            KnownPresentationProperties = registry.PresentationProperties.ToHashSet(StringComparer.OrdinalIgnoreCase),
        };

        if (options.KnownPresentationProperties.Count > 0)
        {
            DashSpec.Modeling.Parse.PropertySchemas.registerPresentationProperties(options.KnownPresentationProperties);
        }

        foreach (var diagramKind in registry.DiagramKinds.Values)
        {
            DashSpec.Modeling.Parse.Diagram.DiagramKindRegistry.registerPluginKind(
                diagramKind.KindId,
                diagramKind.DataFamily,
                diagramKind.SupportsTopLimit,
                diagramKind.BindingProperties);
        }

        foreach (var ruleChecker in registry.RuleCheckers.Values)
        {
            DashboardValidationCollector.RegisterChecker(
                ruleChecker.CheckerId,
                (doc, errors) =>
                {
                    try
                    {
                        ruleChecker.Validate(doc);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex.Message);
                    }
                });
        }

        foreach (var unitKind in registry.UnitKinds.Values)
        {
            DashSpec.Modeling.Parse.Document.UnitProjector.registerCustomProjector(
                unitKind.KindKeyword,
                 Microsoft.FSharp.Core.FSharpFunc<DashSpec.Modeling.Parse.UnitGrammar.UnitEnvelope, object>.FromConverter(
                    env => unitKind.Project(env.Id, null)));
        }

        return options;
    }
}
