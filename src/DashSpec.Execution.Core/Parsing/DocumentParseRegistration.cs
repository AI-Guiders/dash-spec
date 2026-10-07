using System.Linq;
using DashSpec.Core.Analysis;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using CorePhraseTemplate = DashSpec.Abstractions.Plugins.PhraseTemplateDescriptor;
using CorePhraseSlot = DashSpec.Abstractions.Plugins.PhraseSlotDescriptor;
using ParseDoc = DashSpec.Modeling.Parse.Document;
using Microsoft.FSharp.Core;

namespace DashSpec.Execution.Parsing;

/// <summary>Wire F# document parsers into Core bridges (ADR-0048 M4).</summary>
internal static class DocumentParseRegistration
{
    internal static void Register()
    {
        ParseDoc.DashboardValidationBridge.registerAction(document =>
        {
            DashboardValidator.Validate(DocumentModelMapper.ToCore(document));
        });

        DocumentParseBridge.Parse = (text, specDirectory, parseOptions) =>
        {
            try
            {
                var document = ParseDoc.DashboardComposer.parse(
                    text,
                    ToFsharpOption(specDirectory),
                    ToFsharp(parseOptions));
                return DocumentModelMapper.ToCore(document);
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
                return FromFsharpOption(ParseDoc.DashboardParser.readRuntimePath(text));
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
                return FromFsharpOption(ParseDoc.DashboardParser.readRuntimePath(text));
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
                return FromFsharpOption(ParseDoc.DashboardParser.readDiagramLibraryPath(text));
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
                return FromFsharpOption(ParseDoc.DashboardParser.readPalettePath(text));
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
                return ToCore(ParseDoc.DashboardParser.readSqlDialect(text));
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
                var (id, title) = ParseDoc.DashboardParser.readDashboardHeader(text);
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
                return ParseDoc.DocumentModuleParser.isBlockModuleFormat(text);
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };


        DocumentParseBridge.Compile = (text, specDirectory, parseOptions) =>
        {
            try
            {
                var result = ParseDoc.DashSpecCompiler.compile(
                    text,
                    ToFsharpOption(specDirectory),
                    ToFsharp(parseOptions));
                var document = DocumentModelMapper.ToCore(result.Document);
                var diagnostics = result.Diagnostics
                    .Select(ToCoreDiagnostic)
                    .ToList();
                return new DashSpec.Core.Platform.ReportCompileResult(document, diagnostics);
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
                return ParseDoc.DashboardComposer.isTabRootDocument(text);
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

    private static DashSpec.Modeling.Parse.DashSpecParseOptions ToFsharp(DashSpec.Core.Parsing.DashSpecParseOptions options) =>
        new()
        {
            MergeReferencedTabModules = options.MergeReferencedTabModules,
            TolerateIncompleteIncludes = options.TolerateIncompleteIncludes,
            ModuleLinkMode = (DashSpec.Modeling.Parse.Include.ModuleLinkMode)(int)options.ModuleLinkMode,
            LinkOnlyReferencedDiagramUnits = options.LinkOnlyReferencedDiagramUnits,
            AllowLegacyIncludes = options.AllowLegacyIncludes,
            ExtensionBlockKeywords = options.ExtensionBlockKeywords,
            ExtensionBlockPluginIds = options.ExtensionBlockPluginIds,
            PhraseTemplates = options.PhraseTemplates.Select(ToFsharpPhraseTemplate).ToList(),
            KnownActionHandlers = options.KnownActionHandlers,
            KnownInteractionHandlers = options.KnownInteractionHandlers,
        };

    private static DashSpec.Modeling.Parse.PhraseTemplateDescriptor ToFsharpPhraseTemplate(CorePhraseTemplate template) =>
        new()
        {
            PluginId = template.PluginId,
            HandlerId = template.HandlerId,
            Scope = template.Scope,
            Pattern = template.Pattern,
            Slots = template.Slots.Select(ToFsharpPhraseSlot).ToList(),
        };

    private static DashSpec.Modeling.Parse.PhraseSlotDescriptor ToFsharpPhraseSlot(CorePhraseSlot slot) =>
        new()
        {
            Name = slot.Name,
            Kind = (DashSpec.Modeling.Parse.PhraseSlotKind)(int)slot.Kind,
            Optional = slot.Optional,
        };

    private static DashSpec.Core.Validation.DashSpecDiagnostic ToCoreDiagnostic(
        DashSpec.Modeling.Core.DashSpecDiagnostic diagnostic)
    {
        var span = diagnostic.Span;
        var severity = DashSpec.Core.Validation.DashSpecDiagnosticSeverity.Error;
        if (diagnostic.Severity.Equals(DashSpec.Modeling.Core.DashSpecDiagnosticSeverity.Warning))
        {
            severity = DashSpec.Core.Validation.DashSpecDiagnosticSeverity.Warning;
        }
        else if (diagnostic.Severity.Equals(DashSpec.Modeling.Core.DashSpecDiagnosticSeverity.Information))
        {
            severity = DashSpec.Core.Validation.DashSpecDiagnosticSeverity.Information;
        }
        return new DashSpec.Core.Validation.DashSpecDiagnostic(
            span.Line,
            span.Character,
            span.EndLine,
            span.EndCharacter,
            diagnostic.Message,
            severity);
    }

    private static SqlDialect ToCore(ParseDoc.SqlDialect dialect)
    {
        if (dialect.Equals(ParseDoc.SqlDialect.TSql)) return SqlDialect.TSql;
        if (dialect.Equals(ParseDoc.SqlDialect.Postgres)) return SqlDialect.Postgres;
        if (dialect.Equals(ParseDoc.SqlDialect.Generic)) return SqlDialect.Generic;
        throw new ArgumentOutOfRangeException(nameof(dialect), dialect, "Unknown SQL dialect.");
    }
}

