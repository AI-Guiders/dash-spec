using System.Linq;
using AIGuiders.Platform.Modeling.Notations.Keyboard;
using AIGuiders.Platform.Modeling.Paths;
using AIGuiders.Platform.Notations.Keyboard;
using DashSpec.Compiler.Contract;
using DashSpec.Execution.Runtime;
using DashSpec.Modeling.Parse.Hotkeys;
using Microsoft.FSharp.Core;
using CorePhraseSlot = DashSpec.Abstractions.Plugins.PhraseSlotDescriptor;
using CorePhraseTemplate = DashSpec.Abstractions.Plugins.PhraseTemplateDescriptor;
using FsharpImport = DashSpec.Modeling.Parse.Import.UnitFragmentResolver;
using ParseDoc = DashSpec.Modeling.Parse.Document;

namespace DashSpec.Execution.Parsing;

/// <summary>
/// Single unified C# parse &amp; compile facade over F# <c>DashSpec.Modeling.Parse</c> and <c>UnitProjector</c>.
/// </summary>
public static class DashSpecParser
{
    static DashSpecParser()
    {
        RegisterKeyboardNotationBridge();
        ParseDoc.UnitImport.ensureBuiltinUnitsRegistered();
        ParseDoc.DashboardValidationBridge.registerAction(DashboardValidator.Validate);
        RegisterEmbeddedStdlib();
    }

    private static void RegisterKeyboardNotationBridge()
    {
        HotkeyGestureNormalizer.registerExternalParser(
            new HotkeyGestureNormalizer.ExternalParserDelegate(TryParseViaPlatformNotations));
    }

    private static bool TryParseViaPlatformNotations(
        string wire,
        HotkeyNotationKind notation,
        ref NormalizedKeySequence sequence,
        ref string error)
    {
        if (string.IsNullOrWhiteSpace(wire))
        {
            error = "Gesture cannot be empty.";
            return false;
        }

        var trimmed = wire.Trim();

        // Normalize bracketed non-letter keys (e.g. <C-.>, <C-LMB>, <C-RMB>, <C-MMB>) into KeyGestureConfig
        // when VimDocument/NeovimKbd restricts bracket tokens to ASCII letters or named keys.
        static string NormalizeBracketChordToKeyGesture(string token)
        {
            if (token.Length < 3 || token[0] != '<' || token[^1] != '>')
            {
                return token;
            }

            var inner = token[1..^1];
            var mods = new System.Collections.Generic.List<string>(4);
            var changed = false;
            while (inner.Length > 2 && inner[1] == '-')
            {
                var prefix = char.ToUpperInvariant(inner[0]);
                var mapped = prefix switch
                {
                    'C' => "Ctrl",
                    'M' or 'A' => "Alt",
                    'S' => "Shift",
                    'D' or 'H' => "Meta",
                    _ => null,
                };
                if (mapped is null)
                {
                    break;
                }
                mods.Add(mapped);
                inner = inner[2..];
                changed = true;
            }

            return changed && !string.IsNullOrWhiteSpace(inner)
                ? string.Join('+', [.. mods, inner])
                : inner;
        }

        // Delegate to AIGuiders.Platform.Notations.Keyboard.All (KeyboardNotationParser)
        if (notation.IsKeyGesture)
        {
            if (trimmed.StartsWith('<') || trimmed.EndsWith('>'))
            {
                error = $"Notation 'key-gesture' uses 'Ctrl+K' syntax, not '<...>' ('{trimmed}').";
                return false;
            }
            if (KeyboardNotationParser.TryParseToSequence(trimmed, KeyboardNotationSurface.KeyGestureConfig, out var parsed, out var err) &&
                parsed is not null)
            {
                sequence = parsed;
                error = "";
                return true;
            }
            error = err;
            return false;
        }

        if (notation.IsVim || notation.IsNeovimKbd)
        {
            if (trimmed.Contains('+'))
            {
                error = $"Notation '{HotkeyNotationKindModule.toKeyword(notation)}' uses '<C-k>' bracket syntax, not '+' ('{trimmed}').";
                return false;
            }
            var primary = notation.IsVim ? KeyboardNotationSurface.VimDocument : KeyboardNotationSurface.NeovimKbd;
            var secondary = notation.IsVim ? KeyboardNotationSurface.NeovimKbd : KeyboardNotationSurface.VimDocument;
            if (KeyboardNotationParser.TryParseToSequence(trimmed, primary, out var parsed, out var err) &&
                parsed is not null)
            {
                sequence = parsed;
                error = "";
                return true;
            }
            if (KeyboardNotationParser.TryParseToSequence(trimmed, secondary, out parsed, out err) &&
                parsed is not null)
            {
                sequence = parsed;
                error = "";
                return true;
            }
            if (trimmed.StartsWith('<') && trimmed.EndsWith('>'))
            {
                var rewritten = NormalizeBracketChordToKeyGesture(trimmed);
                if (KeyboardNotationParser.TryParseToSequence(rewritten, KeyboardNotationSurface.KeyGestureConfig, out parsed, out err) &&
                    parsed is not null)
                {
                    sequence = parsed;
                    error = "";
                    return true;
                }
            }
            error = err;
            return false;
        }

        if (notation.IsEmacsKbd)
        {
            if (trimmed.Contains('+'))
            {
                error = $"Notation 'emacs-kbd' uses 'C-k' hyphen syntax, not '+' ('{trimmed}').";
                return false;
            }
            if (KeyboardNotationParser.TryParseToSequence(trimmed, KeyboardNotationSurface.EmacsKbd, out var parsed, out var err) &&
                parsed is not null)
            {
                sequence = parsed;
                error = "";
                return true;
            }
            error = err;
            return false;
        }

        // Auto mode: detect surface from wire token shape via KeyboardNotationParser
        if (trimmed.StartsWith('<') && trimmed.EndsWith('>'))
        {
            if (KeyboardNotationParser.TryParseToSequence(trimmed, KeyboardNotationSurface.NeovimKbd, out var parsed, out var err) &&
                parsed is not null)
            {
                sequence = parsed;
                error = "";
                return true;
            }
            if (KeyboardNotationParser.TryParseToSequence(trimmed, KeyboardNotationSurface.VimDocument, out parsed, out err) &&
                parsed is not null)
            {
                sequence = parsed;
                error = "";
                return true;
            }
            var rewritten = NormalizeBracketChordToKeyGesture(trimmed);
            if (KeyboardNotationParser.TryParseToSequence(rewritten, KeyboardNotationSurface.KeyGestureConfig, out parsed, out err) &&
                parsed is not null)
            {
                sequence = parsed;
                error = "";
                return true;
            }
            error = err;
            return false;
        }

        if (trimmed.Contains('+') || trimmed.StartsWith('\u2318'))
        {
            if (KeyboardNotationParser.TryParseToSequence(trimmed, KeyboardNotationSurface.KeyGestureConfig, out var parsed, out var err) &&
                parsed is not null)
            {
                sequence = parsed;
                error = "";
                return true;
            }
            error = err;
            return false;
        }

        if (trimmed.StartsWith("C-", StringComparison.Ordinal) ||
            trimmed.StartsWith("M-", StringComparison.Ordinal) ||
            trimmed.StartsWith("S-", StringComparison.Ordinal) ||
            trimmed.StartsWith("A-", StringComparison.Ordinal) ||
            trimmed.StartsWith("H-", StringComparison.Ordinal) ||
            trimmed.StartsWith("s-", StringComparison.Ordinal))
        {
            if (KeyboardNotationParser.TryParseToSequence(trimmed, KeyboardNotationSurface.EmacsKbd, out var parsed, out var err) &&
                parsed is not null)
            {
                sequence = parsed;
                error = "";
                return true;
            }
            error = err;
            return false;
        }

        if (KeyboardNotationParser.TryParseToSequence(trimmed, KeyboardNotationSurface.KeyGestureConfig, out var plainParsed, out var plainErr) &&
            plainParsed is not null)
        {
            sequence = plainParsed;
            error = "";
            return true;
        }

        error = plainErr;
        return false;
    }

    public static void EnsureModuleParsersRegistered() => _ = typeof(DashSpecParser);

    internal static void EnsureStdlibRegistered() => RegisterEmbeddedStdlib();

    private static void RegisterEmbeddedStdlib()
    {
        const string prefix = "stdlib/";
        var assemblies = new[]
        {
            typeof(DashSpec.Modeling.Core.ImportKindRegistry).Assembly,
            typeof(ParseDoc.UnitSource).Assembly,
            typeof(DashSpecParser).Assembly,
        };
        foreach (var assembly in assemblies.Distinct())
        {
            var units = assembly
                .GetManifestResourceNames()
                .Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(name =>
                {
                    using var stream = assembly.GetManifestResourceStream(name)
                        ?? throw new InvalidOperationException($"Embedded stdlib unit '{name}' not found in manifest.");
                    using var reader = new System.IO.StreamReader(stream);
                    var normalizedName = LogicalPath.Create(name).Value;
                    return System.Tuple.Create(normalizedName, reader.ReadToEnd());
                });
            ParseDoc.UnitSource.registerEmbeddedUnits(units);
        }
    }

    public static string? ReadRuntimePath(string text)
    {
        EnsureModuleParsersRegistered();
        try
        {
            return FromFsharpOption(ParseDoc.DashboardParser.readRuntimePath(text));
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    [Obsolete("Use ReadRuntimePath. @config is a deprecated alias for @runtime.")]
    public static string? ReadConfigPath(string text) => ReadRuntimePath(text);

    public static string? ReadDiagramLibraryPath(string text)
    {
        EnsureModuleParsersRegistered();
        try
        {
            return FromFsharpOption(ParseDoc.DashboardParser.readDiagramLibraryPath(text));
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    public static SqlDialect ReadSqlDialect(string text)
    {
        EnsureModuleParsersRegistered();
        try
        {
            return ParseDoc.DashboardParser.readSqlDialect(text).IsPostgres
                ? SqlDialect.Postgres
                : SqlDialect.TSql;
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    public static (string Id, string Title) ReadDashboardHeader(string text)
    {
        EnsureModuleParsersRegistered();
        try
        {
            var (id, title) = ParseDoc.DashboardParser.readDashboardHeader(text);
            return (id, title);
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    public static bool IsBlockModuleFormat(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        EnsureModuleParsersRegistered();
        try
        {
            return ParseDoc.DocumentModuleParser.isBlockModuleFormat(text);
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    public static bool IsTabRootDocument(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        EnsureModuleParsersRegistered();
        try
        {
            return ParseDoc.DashboardComposer.isTabRootDocument(text);
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    public static DashboardDocument Parse(string text, string? specDirectory = null) =>
        Parse(text, specDirectory, DashSpecParseOptions.Default);

    public static DashboardDocument Parse(
        string text,
        string? specDirectory,
        DashSpecParseOptions parseOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(parseOptions);
        EnsureModuleParsersRegistered();
        try
        {
            return ParseDoc.DashboardComposer.parse(
                text,
                ToFsharpOption(specDirectory),
                ToFsharp(parseOptions));
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    public static ReportCompileResult Compile(string text, string? specDirectory = null) =>
        Compile(text, specDirectory, DashSpecParseOptions.Default);

    public static ReportCompileResult Compile(
        string text,
        string? specDirectory,
        DashSpecParseOptions parseOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(parseOptions);
        EnsureModuleParsersRegistered();
        try
        {
            var result = ParseDoc.DashSpecCompiler.compile(
                text,
                ToFsharpOption(specDirectory),
                ToFsharp(parseOptions));
            var diagnostics = result.Diagnostics
                .Select(ToCoreDiagnostic)
                .ToList();
            return new ReportCompileResult(result.Document, diagnostics);
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    public static HostDocument ParseHost(string text, string? specDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        EnsureModuleParsersRegistered();
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

    public static HostDocument ParseHostFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Host file not found.", path);
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        return ParseHost(File.ReadAllText(path), directory);
    }

    public static LayoutBoardDefinition ParseLayoutFile(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        EnsureModuleParsersRegistered();
        try
        {
            return DashSpec.Modeling.Parse.Layout.LayoutModuleParser.parseLayoutFile(text);
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    public static CatalogDocument ParseCatalog(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        EnsureModuleParsersRegistered();
        try
        {
            return DashSpec.Modeling.Parse.Catalog.CatalogParser.parse(text);
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    public static TooltipDefinition ParseTooltipFile(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        EnsureModuleParsersRegistered();
        try
        {
            return ToCore(DashSpec.Modeling.Parse.Tooltip.TooltipModuleParser.parseTooltipFile(text));
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    public static (string Id, TooltipDefinition Definition) ParseTooltipFileWithId(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        EnsureModuleParsersRegistered();
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

    public static SeriesTransformBlock ParseTransformFile(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        EnsureModuleParsersRegistered();
        try
        {
            return DashSpec.Modeling.Parse.Transform.TransformModuleParser.parseTransformFile(text);
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    public static (string Id, IReadOnlyDictionary<string, string> Properties) ParsePaletteFile(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        EnsureModuleParsersRegistered();
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
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        EnsureModuleParsersRegistered();
        try
        {
            return UnitFragmentMapper.ToCore(FsharpImport.foldDiagramModule(text));
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    internal static (string Id, UnitFragment Fragment) ParseDiagramFileWithId(string text, string? baseDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        EnsureModuleParsersRegistered();
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

    public static PresentationBlock ParsePresentationBlock(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        EnsureModuleParsersRegistered();
        try
        {
            return UnitFragmentMapper.ToCorePresentation(FsharpImport.parsePresentationBlock(text));
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    public static (string Id, PresentationBlock Block) ParsePresentationFileWithId(string text, string? baseDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        EnsureModuleParsersRegistered();
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

    private static FSharpOption<string> ToFsharpOption(string? value) =>
        string.IsNullOrEmpty(value) ? FSharpOption<string>.None : FSharpOption<string>.Some(value);

    private static string? FromFsharpOption(FSharpOption<string> option)
    {
        var items = OptionModule.ToArray(option);
        return items.Length > 0 ? items[0] : null;
    }

    private static DashSpec.Modeling.Parse.DashSpecParseOptions ToFsharp(DashSpecParseOptions options) =>
        new()
        {
            MergeReferencedTabModules = options.MergeReferencedTabModules,
            ExtensionBlockKeywords = options.ExtensionBlockKeywords,
            ExtensionBlockPluginIds = options.ExtensionBlockPluginIds,
            PhraseTemplates = options.PhraseTemplates.Select(ToFsharpPhraseTemplate).ToList(),
            KnownActionHandlers = options.KnownActionHandlers,
            KnownInteractionHandlers = options.KnownInteractionHandlers,
            KnownClickSources = options.KnownClickSources,
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

    private static DashSpecDiagnostic ToCoreDiagnostic(
        DashSpec.Modeling.Core.DashSpecDiagnostic diagnostic)
    {
        var span = diagnostic.Span;
        var severity = DashSpecDiagnosticSeverity.Error;
        if (diagnostic.Severity.Equals(DashSpec.Modeling.Core.DashSpecDiagnosticSeverity.Warning))
        {
            severity = DashSpecDiagnosticSeverity.Warning;
        }
        else if (diagnostic.Severity.Equals(DashSpec.Modeling.Core.DashSpecDiagnosticSeverity.Information))
        {
            severity = DashSpecDiagnosticSeverity.Information;
        }

        return new DashSpecDiagnostic(
            span.Line,
            span.Character,
            span.EndLine,
            span.EndCharacter,
            diagnostic.Message,
            severity);
    }
}
