using System.Linq;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using Microsoft.FSharp.Core;
using FsharpCatalog = DashSpec.Modeling.Parse.Catalog.CatalogDocument;
using FsharpCatalogEntry = DashSpec.Modeling.Parse.Catalog.CatalogEntryDefinition;
using FsharpCatalogGroup = DashSpec.Modeling.Parse.Catalog.CatalogGroupDefinition;
using FsharpBoard = DashSpec.Modeling.Parse.Layout.LayoutBoardDefinition;
using FsharpScope = DashSpec.Modeling.Parse.Layout.LayoutScope;
using FsharpTooltip = DashSpec.Modeling.Parse.Tooltip.TooltipDefinition;
using FsharpTransform = DashSpec.Modeling.Parse.Transform.SeriesTransformBlock;
using FsharpPalette = DashSpec.Modeling.Parse.Palette.PaletteDocument;
using FsharpPresentation = DashSpec.Modeling.Parse.Presentation.PresentationModuleDocument;
using FsharpPresentationBlock = DashSpec.Modeling.Parse.Presentation.PresentationBlock;
using FsharpDiagram = DashSpec.Modeling.Parse.Diagram.DiagramDefinition;
using FsharpDiagramStmt = DashSpec.Modeling.Parse.Diagram.DiagramFragmentStatement;
using FsharpInspect = DashSpec.Modeling.Parse.Diagram.InspectPresentation;

namespace DashSpec.Execution.Parsing;

/// <summary>Wire F# fragment parsers into Core bridges (ADR-0048 M2–M5).</summary>
internal static class ModuleParseRegistration
{
    static ModuleParseRegistration()
    {
        DocumentParseRegistration.Register();
        RegisterLayout();
        RegisterTooltip();
        RegisterCatalog();
        RegisterTransform();
        RegisterPalette();
        RegisterPresentation();
        RegisterDiagram();
    }

    internal static void EnsureRegistered() => _ = typeof(ModuleParseRegistration);

    private static void RegisterLayout()
    {
        LayoutParseBridge.ParseLayoutFile = text =>
        {
            try
            {
                return ToCore(DashSpec.Modeling.Parse.Layout.LayoutModuleParser.parseLayoutFile(text));
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };
    }

    private static void RegisterTooltip()
    {
        TooltipParseBridge.ParseTooltipFile = text =>
        {
            try
            {
                return ToCore(DashSpec.Modeling.Parse.Tooltip.TooltipModuleParser.parseTooltipFile(text));
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };

        TooltipParseBridge.ParseTooltipFileWithId = text =>
        {
            try
            {
                var (id, def) = DashSpec.Modeling.Parse.Tooltip.TooltipModuleParser.parseTooltipFileWithId(text);
                return (id, ToCore(def));
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };

        TooltipParseBridge.ParseTooltipBody = (id, body) =>
        {
            try
            {
                return ToCore(DashSpec.Modeling.Parse.Tooltip.TooltipModuleParser.parseTooltipInlineBody(id, body));
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };
    }

    private static void RegisterCatalog()
    {
        CatalogParseBridge.Parse = text =>
        {
            try
            {
                return ToCore(DashSpec.Modeling.Parse.Catalog.CatalogParser.parse(text));
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };
    }

    private static void RegisterTransform()
    {
        TransformParseBridge.ParseTransformFile = text =>
        {
            try
            {
                return ToCore(DashSpec.Modeling.Parse.Transform.TransformModuleParser.parseTransformFile(text));
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };
    }

    private static void RegisterPalette()
    {
        PaletteParseBridge.ParsePaletteFile = text =>
        {
            try
            {
                var doc = DashSpec.Modeling.Parse.Palette.PaletteModuleParser.parsePaletteFile(text);
                return (doc.Id, ToCore(doc));
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };
    }

    private static void RegisterDiagram()
    {
        DiagramParseBridge.ParseDiagramFile = (text, baseDirectory) =>
            FoldDiagramStatements(
                DashSpec.Modeling.Parse.Diagram.DiagramModuleParser.parseDiagramModule(text).Statements,
                baseDirectory);

        DiagramParseBridge.ParseDiagramFileWithId = (text, baseDirectory) =>
        {
            var (id, doc) = DashSpec.Modeling.Parse.Diagram.DiagramModuleParser.parseDiagramModuleWithId(text);
            return (id, FoldDiagramStatements(doc.Statements, baseDirectory));
        };
    }

    private static SpecIncludeFragment FoldDiagramStatements(
        IReadOnlyList<FsharpDiagramStmt> statements,
        string? baseDirectory)
    {
        try
        {
            SpecIncludeFragment fragment = new(null, null, null);

            foreach (var statement in statements)
            {
                switch (statement)
                {
                    case FsharpDiagramStmt.IncludeStatement include:
                        if (string.IsNullOrWhiteSpace(baseDirectory))
                        {
                            throw new DashSpecParseException(
                                "Diagram include requires a base directory (parse from file path).");
                        }

                        fragment = SpecIncludeResolver.Merge(
                            fragment,
                            SpecIncludeResolver.Load(include.Item.Kind, include.Item.Reference, baseDirectory));
                        break;

                    case FsharpDiagramStmt.DiagramStatement diagram:
                        fragment = SpecIncludeResolver.Merge(
                            fragment,
                            new SpecIncludeFragment(ToCore(diagram.Item), null, null));
                        break;

                    case FsharpDiagramStmt.PresentationStatement presentation:
                        fragment = SpecIncludeResolver.Merge(
                            fragment,
                            new SpecIncludeFragment(null, ToCore(presentation.Item), null));
                        break;

                    case FsharpDiagramStmt.SeriesTransformStatement transform:
                        fragment = SpecIncludeResolver.Merge(
                            fragment,
                            new SpecIncludeFragment(null, null, ToCore(transform.Item)));
                        break;

                    case FsharpDiagramStmt.TooltipStatement tooltip:
                        fragment = SpecIncludeResolver.Merge(
                            fragment,
                            new SpecIncludeFragment(
                                null,
                                null,
                                null,
                                new Dictionary<string, TooltipDefinition>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [tooltip.Item1] = ToCore(tooltip.Item2),
                                }));
                        break;

                    case FsharpDiagramStmt.InspectStatement inspect:
                        fragment = SpecIncludeResolver.Merge(
                            fragment,
                            new SpecIncludeFragment(null, null, null, Inspect: ToCore(inspect.Item)));
                        break;
                }
            }

            if (fragment.Diagram is null)
            {
                throw new DashSpecParseException(
                    "Diagram module requires a chart kind block (e.g. heatmap … end heatmap).");
            }

            return fragment;
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    private static void RegisterPresentation()
    {
        PresentationParseBridge.ParsePresentationFile = (text, baseDirectory) =>
            ParsePresentationModule(text, baseDirectory).Block;

        PresentationParseBridge.ParsePresentationFileWithId = (text, baseDirectory) =>
        {
            var result = ParsePresentationModule(text, baseDirectory);
            return (result.Id, result.Block);
        };
    }

    private static (string Id, PresentationBlock Block) ParsePresentationModule(string text, string? baseDirectory)
    {
        try
        {
            var doc = DashSpec.Modeling.Parse.Presentation.PresentationModuleParser.parsePresentationModule(text);
            PresentationBlock? merged = null;

            foreach (var include in doc.Includes)
            {
                if (string.IsNullOrWhiteSpace(baseDirectory))
                {
                    throw new DashSpecParseException(
                        "Presentation include requires a base directory (parse from file path).");
                }

                if (!PresentationModuleParser.IsChartChromeIncludeKind(include.Kind))
                {
                    throw new DashSpecParseException(
                        $"@presentation module only supports include presentation/chrome, got '{include.Kind}'.");
                }

                var fragment = SpecIncludeResolver.Load(include.Kind, include.Reference, baseDirectory);
                merged = SpecIncludeResolver.Merge(
                    new SpecIncludeFragment(null, merged, null),
                    fragment).Presentation;
            }

            var localItems = OptionModule.ToArray(doc.Local);
            PresentationBlock? local = localItems.Length > 0 ? ToCore(localItems[0]) : null;
            var block = SpecIncludeResolver.Merge(
                new SpecIncludeFragment(null, merged, null),
                new SpecIncludeFragment(null, local, null)).Presentation;

            return (doc.Id, block ?? throw new DashSpecParseException("@presentation module requires at least one property."));
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    private static TooltipDefinition ToCore(FsharpTooltip tooltip)
    {
        var definition = new TooltipDefinition(tooltip.Id, tooltip.Variables, tooltip.Template);
        try
        {
            TooltipTemplate.Validate(definition);
        }
        catch (InvalidOperationException ex)
        {
            throw new DashSpecParseException(ex.Message);
        }

        return definition;
    }

    private static CatalogDocument ToCore(FsharpCatalog catalog)
    {
        var entries = catalog.Entries
            .Select(e => new CatalogEntryDefinition(
                e.Id,
                e.Title,
                e.DashspecPath,
                OptionModule.ToArray(e.GroupId).FirstOrDefault()))
            .ToList();

        IReadOnlyList<CatalogGroupDefinition>? groups = null;
        var groupItems = OptionModule.ToArray(catalog.Groups);
        if (groupItems.Length > 0)
        {
            groups = groupItems[0]
                .Select(g => new CatalogGroupDefinition(g.Id, g.Title))
                .ToList();
        }

        return new CatalogDocument(catalog.Id, catalog.DefaultEntryId, entries, groups);
    }

    private static SeriesTransformBlock ToCore(FsharpTransform transform) =>
        new(
            OptionModule.ToArray(transform.UsePreset).FirstOrDefault(),
            OptionModule.ToArray(transform.Max).FirstOrDefault(),
            OptionModule.ToArray(transform.OtherLabel).FirstOrDefault());

    private static IReadOnlyDictionary<string, string> ToCore(FsharpPalette palette) =>
        palette.Properties.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

    private static PresentationBlock ToCore(FsharpPresentationBlock block) =>
        new(
            OptionModule.ToArray(block.UsePreset).FirstOrDefault(),
            block.Properties.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase));

    private static DiagramDefinition ToCore(FsharpDiagram diagram)
    {
        var usePreset = OptionModule.ToArray(diagram.UsePreset).FirstOrDefault();
        return new DiagramDefinition(
            diagram.Kind,
            diagram.Properties.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            usePreset);
    }

    private static InspectPresentation ToCore(FsharpInspect inspect) =>
        new(
            OptionModule.ToArray(inspect.TooltipId).FirstOrDefault(),
            OptionModule.ToArray(inspect.Label).FirstOrDefault(),
            inspect.Format,
            inspect.Split);

    private static LayoutScope? MapScope(FsharpScope scope)
    {
        if (scope.Equals(FsharpScope.Toolbar)) return LayoutScope.Toolbar;
        if (scope.Equals(FsharpScope.Tab)) return LayoutScope.Tab;
        if (scope.Equals(FsharpScope.Page)) return LayoutScope.Page;
        if (scope.Equals(FsharpScope.Card)) return LayoutScope.Card;
        return null;
    }

    private static LayoutBoardDefinition ToCore(FsharpBoard board)
    {
        var scopes = OptionModule.ToArray(board.ModuleScope);
        LayoutScope? moduleScope = scopes.Length > 0 ? MapScope(scopes[0]) : null;
        return new LayoutBoardDefinition(board.Rows, moduleScope);
    }
}
