using System.Linq;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using Microsoft.FSharp.Core;
using FsharpCatalog = DashSpec.Modeling.Parse.Catalog.CatalogDocument;
using FsharpCatalogEntry = DashSpec.Modeling.Parse.Catalog.CatalogEntryDefinition;
using FsharpCatalogGroup = DashSpec.Modeling.Parse.Catalog.CatalogGroupDefinition;
using FsharpTooltip = DashSpec.Modeling.Parse.Tooltip.TooltipDefinition;
using FsharpTransform = DashSpec.Modeling.Parse.Transform.SeriesTransformBlock;

namespace DashSpec.Execution.Parsing;

/// <summary>Wire F# fragment parsers into Core bridges (ADR-0048 M5).</summary>
internal static class ModuleParseRegistration
{
    static ModuleParseRegistration()
    {
        RegisterLayout();
        RegisterTooltip();
        RegisterCatalog();
        RegisterTransform();
    }

    internal static void EnsureRegistered() => _ = typeof(ModuleParseRegistration);

    private static void RegisterLayout() => LayoutParseRegistration.EnsureRegistered();

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
}
