using DashSpec.Execution.Runtime;
using DashSpec.Compiler.Contract;

namespace DashSpec.Composition.Plugins;

public sealed class DashSpecParseOptionsProvider(DashSpecContributorRegistry registry)
{
    public DashSpecParseOptions CreateOptions()
    {
        var knownPresentationProperties = registry.PresentationProperties.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (registry.EmbeddedUnits.Count > 0)
        {
            foreach (var (_, content) in registry.EmbeddedUnits)
            {
                var tree = DashSpec.Modeling.Parse.Syntax.DashSpecSurfaceSyntax.parse(content);
                foreach (var node in DashSpec.Modeling.Parse.Syntax.DashSpecAst.descendants(tree.Root))
                {
                    if (node is DashSpec.Modeling.Parse.Syntax.DashSpecAstNode.PropertyAssignment { Item: var prop } &&
                        !prop.KeyPath.IsQualified &&
                        !string.IsNullOrWhiteSpace(prop.Key))
                    {
                        knownPresentationProperties.Add(prop.Key);
                    }
                }
            }

            DashSpec.Modeling.Parse.Document.UnitSource.registerEmbeddedUnits(
                registry.EmbeddedUnits.Select(x => System.Tuple.Create(x.LogicalPath, x.Content)));
        }

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
            KnownPresentationProperties = knownPresentationProperties,
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
            DashSpec.Modeling.Parse.UnitProjector.registerCustomProjector(
                unitKind.KindKeyword,
                Microsoft.FSharp.Core.FSharpFunc<DashSpec.Modeling.Parse.UnitGrammar.UnitEnvelope, object>.FromConverter(
                    env => unitKind.Project(env.Id, null)));
        }

        return options;
    }
}
