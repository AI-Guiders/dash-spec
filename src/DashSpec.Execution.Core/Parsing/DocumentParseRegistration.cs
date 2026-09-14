using System.Linq;
using DashSpec.Abstractions.Plugins;
using DashSpec.Core.Analysis;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using Microsoft.FSharp.Core;
using FsharpDocument = DashSpec.Modeling.Parse.Document;
using FsharpParseOptions = DashSpec.Modeling.Parse.DashSpecParseOptions;
using FsharpPhrase = DashSpec.Modeling.Parse;

namespace DashSpec.Execution.Parsing;

/// <summary>Wire F# document parsers into Core bridges (ADR-0048 M4).</summary>
internal static class DocumentParseRegistration
{
    internal static void Register()
    {
        DocumentParseBridge.Parse = (text, specDirectory, parseOptions) =>
        {
            try
            {
                var document = FsharpDocument.DashboardComposer.parse(
                    text,
                    ToFsharpOption(specDirectory),
                    ToFsharp(parseOptions));
                var core = DocumentModelMapper.ToCore(document);
                DashboardValidator.Validate(core);
                return core;
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };

        DocumentParseBridge.ReadRuntimePath = text =>
        {
            try
            {
                return FromFsharpOption(FsharpDocument.DashboardParser.readRuntimePath(text));
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };

        DocumentParseBridge.ReadConfigPath = text =>
        {
            try
            {
                return FromFsharpOption(FsharpDocument.DashboardParser.readRuntimePath(text));
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };

        DocumentParseBridge.ReadDiagramLibraryPath = text =>
        {
            try
            {
                return FromFsharpOption(FsharpDocument.DashboardParser.readDiagramLibraryPath(text));
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };

        DocumentParseBridge.ReadPalettePath = text =>
        {
            try
            {
                return FromFsharpOption(FsharpDocument.DashboardParser.readPalettePath(text));
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };

        DocumentParseBridge.ReadSqlDialect = text =>
        {
            try
            {
                return ToCore(FsharpDocument.DashboardParser.readSqlDialect(text));
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };

        DocumentParseBridge.ReadDashboardHeader = text =>
        {
            try
            {
                var (id, title) = FsharpDocument.DashboardParser.readDashboardHeader(text);
                return (id, title);
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };

        DocumentParseBridge.IsBlockModuleFormat = text =>
        {
            try
            {
                return FsharpDocument.DocumentModuleParser.isBlockModuleFormat(text);
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };

        DocumentParseBridge.IsTabRootDocument = text =>
        {
            try
            {
                return FsharpDocument.DashboardComposer.isTabRootDocument(text);
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };
    }

    private static FSharpOption<string> ToFsharpOption(string? value) =>
        string.IsNullOrEmpty(value) ? FSharpOption<string>.None : FSharpOption<string>.Some(value);

    private static string? FromFsharpOption(FSharpOption<string> option)
    {
        var items = OptionModule.ToArray(option);
        return items.Length > 0 ? items[0] : null;
    }

    private static FsharpParseOptions ToFsharp(DashSpecParseOptions options) =>
        new()
        {
            MergeReferencedTabModules = options.MergeReferencedTabModules,
            TolerateIncompleteIncludes = options.TolerateIncompleteIncludes,
            ExtensionBlockKeywords = options.ExtensionBlockKeywords,
            ExtensionBlockPluginIds = options.ExtensionBlockPluginIds,
            PhraseTemplates = options.PhraseTemplates.Select(ToFsharp).ToList(),
            KnownActionHandlers = options.KnownActionHandlers,
            KnownInteractionHandlers = options.KnownInteractionHandlers,
        };

    private static FsharpPhrase.PhraseTemplateDescriptor ToFsharp(PhraseTemplateDescriptor template) =>
        new()
        {
            PluginId = template.PluginId,
            HandlerId = template.HandlerId,
            Scope = template.Scope,
            Pattern = template.Pattern,
            Slots = template.Slots.Select(ToFsharp).ToList(),
        };

    private static FsharpPhrase.PhraseSlotDescriptor ToFsharp(PhraseSlotDescriptor slot) =>
        new()
        {
            Name = slot.Name,
            Kind = (FsharpPhrase.PhraseSlotKind)(int)slot.Kind,
            Optional = slot.Optional,
        };

    private static SqlDialect ToCore(FsharpDocument.SqlDialect dialect)
    {
        if (dialect.Equals(FsharpDocument.SqlDialect.TSql)) return SqlDialect.TSql;
        if (dialect.Equals(FsharpDocument.SqlDialect.Postgres)) return SqlDialect.Postgres;
        if (dialect.Equals(FsharpDocument.SqlDialect.Generic)) return SqlDialect.Generic;
        throw new ArgumentOutOfRangeException(nameof(dialect), dialect, "Unknown SQL dialect.");
    }
}
