using System.Linq;
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
using FsharpImport = DashSpec.Modeling.Parse.Import.UnitFragmentResolver;

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

    /// <summary>Bridge entry for unit text: a `.ds` unit still carries its ADR-0098 header (`namespace` / `import`)
    /// while every unit parser reads only the body, so the header is blanked here once — same length, same offsets.</summary>
    private static string UnitText(string text) =>
        DashSpec.Modeling.Parse.Document.UnitSource.blankHeader(text);

    private static void RegisterLayout()
    {
        LayoutParseBridge.ParseLayoutFile = text =>
        {
            try
            {
                return ToCore(DashSpec.Modeling.Parse.Layout.LayoutModuleParser.parseLayoutFile(UnitText(text)));
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
                return ToCore(DashSpec.Modeling.Parse.Tooltip.TooltipModuleParser.parseTooltipFile(UnitText(text)));
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
                var (id, def) = DashSpec.Modeling.Parse.Tooltip.TooltipModuleParser.parseTooltipFileWithId(UnitText(text));
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
                return ToCore(DashSpec.Modeling.Parse.Catalog.CatalogParser.parse(UnitText(text)));
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
                return ToCore(DashSpec.Modeling.Parse.Host.HostModuleParser.parse(UnitText(text), directory));
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
                return ToCore(DashSpec.Modeling.Parse.Transform.TransformModuleParser.parseTransformFile(UnitText(text)));
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
                var doc = DashSpec.Modeling.Parse.Palette.PaletteModuleParser.parsePaletteFile(UnitText(text));
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
                return UnitFragmentMapper.ToCore(FsharpImport.foldDiagramModule(UnitText(text)));
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
                var (id, fragment) = FsharpImport.foldDiagramModuleWithId(UnitText(text));
                return (id, UnitFragmentMapper.ToCore(fragment));
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };
    }

    private static void RegisterPresentation()
    {
        PresentationParseBridge.ParsePresentationFile = (text, _) =>
            ParsePresentationBlock(text);

        PresentationParseBridge.ParsePresentationFileWithId = (text, _) =>
        {
            try
            {
                var doc = DashSpec.Modeling.Parse.Presentation.PresentationModuleParser.parsePresentationModule(UnitText(text));
                return (doc.Id, ParsePresentationBlock(text));
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };
    }

    private static PresentationBlock ParsePresentationBlock(string text)
    {
        try
        {
            return UnitFragmentMapper.ToCorePresentation(
                FsharpImport.parsePresentationBlock(UnitText(text)));
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

    private static HostDocument ToCore(FsharpHost host) =>
        host;


    private static CatalogDocument ToCore(FsharpCatalog catalog) => catalog;

    private static SeriesTransformBlock ToCore(FsharpTransform transform) =>
        transform;

    private static IReadOnlyDictionary<string, string> ToCore(FsharpPalette palette) =>
        palette.Properties.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

    private static PresentationBlock ToCore(FsharpPresentationBlock block) =>
        block;

    private static DiagramDefinition ToCore(FsharpDiagram diagram) =>
        diagram;

    private static InspectPresentation ToCore(FsharpInspect inspect) =>
        inspect;

    private static LayoutBoardDefinition ToCore(FsharpBoard board) =>
        board;
}
