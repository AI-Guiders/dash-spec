using System.Linq;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using Microsoft.FSharp.Core;
using FsharpCatalog = DashSpec.Modeling.Parse.Catalog.CatalogDocument;
using FsharpCatalogEntry = DashSpec.Modeling.Parse.Catalog.CatalogEntryDefinition;
using FsharpCatalogGroup = DashSpec.Modeling.Parse.Catalog.CatalogGroupDefinition;
using FsharpBoard = DashSpec.Modeling.Parse.Layout.LayoutBoardDefinition;
using FsharpLayout = DashSpec.Modeling.Parse.Layout;
using FsharpScope = DashSpec.Modeling.Parse.Layout.LayoutScope;
using FsharpTooltip = DashSpec.Modeling.Parse.Tooltip.TooltipDefinition;
using FsharpTransform = DashSpec.Modeling.Parse.Transform.SeriesTransformBlock;
using FsharpPalette = DashSpec.Modeling.Parse.Palette.PaletteDocument;
using FsharpPresentation = DashSpec.Modeling.Parse.Presentation.PresentationModuleDocument;
using FsharpPresentationBlock = DashSpec.Modeling.Parse.Presentation.PresentationBlock;
using FsharpHost = DashSpec.Modeling.Parse.Host.HostDocument;
using FsharpHostLink = DashSpec.Modeling.Parse.Host.HostLinkDefinition;
using FsharpDiagram = DashSpec.Modeling.Parse.Diagram.DiagramDefinition;
using FsharpDiagramStmt = DashSpec.Modeling.Parse.Diagram.DiagramFragmentStatement;
using FsharpInspect = DashSpec.Modeling.Parse.Diagram.InspectPresentation;
using FsharpInclude = DashSpec.Modeling.Parse.Include.SpecIncludeFragmentResolver;

namespace DashSpec.Execution.Parsing;

/// <summary>Wire F# fragment parsers into Core bridges (ADR-0048 M2–M5).</summary>
internal static class ModuleParseRegistration
{
    static ModuleParseRegistration()
    {
        DocumentParseRegistration.Register();
        DocumentFormatRegistration.Register();
        DocumentSyntaxRegistration.Register();
        RegisterLayout();
        RegisterTooltip();
        RegisterCatalog();
        RegisterHost();
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

    private static void RegisterHost()
    {
        HostParseBridge.Parse = (text, specDirectory) =>
        {
            try
            {
                var directory =
                    string.IsNullOrWhiteSpace(specDirectory) ? null : specDirectory;
                return ToCore(DashSpec.Modeling.Parse.Host.HostModuleParser.parse(text, directory));
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
        {
            try
            {
                var directory = ResolveDiagramBaseDirectory(text, baseDirectory);
                return SpecIncludeFragmentMapper.ToCore(FsharpInclude.foldDiagramModule(text, directory));
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };

        DiagramParseBridge.ParseDiagramFileWithId = (text, baseDirectory) =>
        {
            try
            {
                var directory = ResolveDiagramBaseDirectory(text, baseDirectory);
                var (id, fragment) = FsharpInclude.foldDiagramModuleWithId(text, directory);
                return (id, SpecIncludeFragmentMapper.ToCore(fragment));
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };
    }

    private static string ResolveDiagramBaseDirectory(string text, string? baseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(baseDirectory))
        {
            return baseDirectory;
        }

        var doc = DashSpec.Modeling.Parse.Diagram.DiagramModuleParser.parseDiagramModule(text);
        foreach (var statement in doc.Statements)
        {
            if (statement is FsharpDiagramStmt.IncludeStatement)
            {
                throw new DashSpecParseException(
                    "Diagram include requires a base directory (parse from file path).");
            }
        }

        return ".";
    }

    private static void RegisterPresentation()
    {
        PresentationParseBridge.ParsePresentationFile = (text, baseDirectory) =>
            ParsePresentationBlock(text, baseDirectory);

        PresentationParseBridge.ParsePresentationFileWithId = (text, baseDirectory) =>
        {
            try
            {
                var doc = DashSpec.Modeling.Parse.Presentation.PresentationModuleParser.parsePresentationModule(text);
                return (doc.Id, ParsePresentationBlock(text, baseDirectory));
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };
    }

    private static PresentationBlock ParsePresentationBlock(string text, string? baseDirectory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                var doc = DashSpec.Modeling.Parse.Presentation.PresentationModuleParser.parsePresentationModule(text);
                if (doc.Includes.Count > 0)
                {
                    throw new DashSpecParseException(
                        "Presentation include requires a base directory (parse from file path).");
                }

                var localItems = OptionModule.ToArray(doc.Local);
                return localItems.Length > 0
                    ? SpecIncludeFragmentMapper.ToCorePresentation(localItems[0])
                    : throw new DashSpecParseException("@presentation module requires at least one property.");
            }

            return SpecIncludeFragmentMapper.ToCorePresentation(
                FsharpInclude.parsePresentationBlockFromFile(text, baseDirectory));
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

    private static HostDocument ToCore(FsharpHost host)
    {
        var links = host.Links
            .Select(link => new HostLinkDefinition(
                link.Id,
                link.Label,
                link.Url,
                link.Target,
                link.Topbar,
                link.Settings))
            .ToList();

        LayoutBoardDefinition? topbarLayout = null;
        var layoutItems = OptionModule.ToArray(host.TopbarLayout);
        if (layoutItems.Length > 0)
        {
            topbarLayout = ToCore(layoutItems[0]);
        }

        return new HostDocument(
            host.Id,
            host.CatalogPath,
            host.Configuration.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            host.Presentation.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            links,
            host.Surfaces.ToList(),
            topbarLayout);
    }

    private static CatalogDocument ToCore(FsharpCatalog catalog)
    {
        var entries = catalog.Entries
            .Select(e => new CatalogEntryDefinition(
                e.Id,
                e.Title,
                e.DashspecPath,
                OptionModule.ToArray(e.GroupId).FirstOrDefault(),
                OptionModule.ToArray(e.InitialTabId).FirstOrDefault()))
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
        if (scope.Equals(FsharpScope.Host)) return LayoutScope.Host;
        return null;
    }

    private static LayoutBoardDefinition ToCore(FsharpBoard board)
    {
        var scopes = OptionModule.ToArray(board.ModuleScope);
        LayoutScope? moduleScope = scopes.Length > 0 ? MapScope(scopes[0]) : null;
        return new LayoutBoardDefinition(
            board.Entries.Select(ToLayoutEntry).ToList(),
            moduleScope);
    }

    private static LayoutBoardEntry ToLayoutEntry(FsharpLayout.LayoutBoardEntry entry) =>
        entry switch
        {
            FsharpLayout.LayoutBoardEntry.CardRow cardRow =>
                new LayoutBoardCardRow(cardRow.Item.ToList()),
            FsharpLayout.LayoutBoardEntry.GroupRow groupRow =>
                new LayoutBoardGroupRow(ToLayoutGroup(groupRow.Item)),
            _ => throw new InvalidOperationException($"Unknown layout board entry: {entry}")
        };

    private static LayoutBoardGroupDefinition ToLayoutGroup(FsharpLayout.LayoutBoardGroupDefinition group) =>
        new(
            group.Id,
            OptionModule.ToArray(group.Title).FirstOrDefault(),
            group.Rows.Select(static row => (IReadOnlyList<string>)row.ToList()).ToList());
}

