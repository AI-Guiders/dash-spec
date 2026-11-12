using System.Linq;
using DashSpec.Execution.Runtime;
using FsharpImport = DashSpec.Modeling.Parse.Import.UnitFragmentResolver;

namespace DashSpec.Execution.Parsing;

internal static class ModuleParseRegistration
{
    static ModuleParseRegistration()
    {
        DocumentParseRegistration.Register();
        DocumentFormatRegistration.Register();
        DocumentSyntaxRegistration.Register();
        RegisterEmbeddedStdlib();
    }

    internal static void EnsureRegistered() => _ = typeof(ModuleParseRegistration);

    internal static void EnsureStdlibRegistered() => RegisterEmbeddedStdlib();

    private static void RegisterEmbeddedStdlib()
    {
        const string prefix = "stdlib/";
        var assembly = typeof(ModuleParseRegistration).Assembly;
        var units = assembly
            .GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)
                    ?? throw new InvalidOperationException($"Embedded stdlib unit '{name}' not found in manifest.");
                using var reader = new System.IO.StreamReader(stream);
                return System.Tuple.Create(name, reader.ReadToEnd());
            });
        DashSpec.Modeling.Parse.Document.UnitSource.registerEmbeddedUnits(units);
    }

    internal static LayoutBoardDefinition ParseLayoutFile(string text)
    {
        EnsureRegistered();
        try
        {
            return DashSpec.Modeling.Parse.Layout.LayoutModuleParser.parseLayoutFile(text);
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    internal static TooltipDefinition ParseTooltipFile(string text)
    {
        EnsureRegistered();
        try
        {
            return ToCore(DashSpec.Modeling.Parse.Tooltip.TooltipModuleParser.parseTooltipFile(text));
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    internal static (string Id, TooltipDefinition Definition) ParseTooltipFileWithId(string text)
    {
        EnsureRegistered();
        try
        {
            var (id, def) = DashSpec.Modeling.Parse.Tooltip.TooltipModuleParser.parseTooltipFileWithId(text);
            return (id, ToCore(def));
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    internal static TooltipDefinition ParseTooltipBody(string id, string body)
    {
        EnsureRegistered();
        try
        {
            return ToCore(DashSpec.Modeling.Parse.Tooltip.TooltipModuleParser.parseTooltipInlineBody(id, body));
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    internal static CatalogDocument ParseCatalog(string text)
    {
        EnsureRegistered();
        try
        {
            return DashSpec.Modeling.Parse.Catalog.CatalogParser.parse(text);
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    internal static HostDocument ParseHost(string text, string? specDirectory)
    {
        EnsureRegistered();
        try
        {
            var directory = string.IsNullOrWhiteSpace(specDirectory) ? null : specDirectory;
            return DashSpec.Modeling.Parse.Host.HostModuleParser.parse(text, directory);
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    internal static SeriesTransformBlock ParseTransformFile(string text)
    {
        EnsureRegistered();
        try
        {
            return DashSpec.Modeling.Parse.Transform.TransformModuleParser.parseTransformFile(text);
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    internal static (string Id, IReadOnlyDictionary<string, string> Properties) ParsePaletteFile(string text)
    {
        EnsureRegistered();
        try
        {
            var doc = DashSpec.Modeling.Parse.Palette.PaletteModuleParser.parsePaletteFile(text);
            return (doc.Id, doc.Properties.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase));
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    internal static UnitFragment ParseDiagramFile(string text)
    {
        EnsureRegistered();
        try
        {
            return UnitFragmentMapper.ToCore(FsharpImport.foldDiagramModule(text));
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    internal static (string Id, UnitFragment Fragment) ParseDiagramFileWithId(string text)
    {
        EnsureRegistered();
        try
        {
            var (id, fragment) = FsharpImport.foldDiagramModuleWithId(text);
            return (id, UnitFragmentMapper.ToCore(fragment));
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    internal static PresentationBlock ParsePresentationBlock(string text)
    {
        EnsureRegistered();
        try
        {
            return UnitFragmentMapper.ToCorePresentation(FsharpImport.parsePresentationBlock(text));
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    internal static (string Id, PresentationBlock Block) ParsePresentationFileWithId(string text)
    {
        EnsureRegistered();
        try
        {
            var doc = DashSpec.Modeling.Parse.Presentation.PresentationModuleParser.parsePresentationModule(text);
            return (doc.Id, ParsePresentationBlock(text));
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    private static TooltipDefinition ToCore(DashSpec.Modeling.Parse.Tooltip.TooltipDefinition tooltip)
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
}
