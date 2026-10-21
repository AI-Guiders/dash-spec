using DashSpec.Execution.Parsing;
using DashSpec.Execution.Runtime;
using FsharpDocument = DashSpec.Modeling.Parse.Document;


using DashSpec.Compiler.Contract;

namespace DashSpec.Execution.Compilation;

public static class DashSpecDiagnosticService
{
    public static IReadOnlyList<DashSpecDiagnostic> ValidateFile(
        string path,
        string? specDirectory = null,
        DashSpecParseOptions? parseOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        parseOptions ??= DashSpecParseOptions.Editor;
        if (!File.Exists(path))
        {
            return
            [
                new DashSpecDiagnostic(0, 0, 0, 1, $"File not found: {path}"),
            ];
        }

        var text = File.ReadAllText(path);
        var directory = specDirectory ?? Path.GetDirectoryName(path)!;
        return ValidateText(text, path, directory, parseOptions);
    }

    public static IReadOnlyList<DashSpecDiagnostic> ValidateText(
        string text,
        string filePath,
        string specDirectory,
        DashSpecParseOptions? parseOptions = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(specDirectory);
        parseOptions ??= DashSpecParseOptions.Editor;

        try
        {
            return ValidateByFileKind(text, filePath, specDirectory, parseOptions);
        }
        catch (DashSpecParseException ex)
        {
            return [TextPositions.ToDiagnostic(text, ex.Message, ex.SourceOffset)];
        }
        catch (FileNotFoundException ex)
        {
            return [MissingFileDiagnostic(text, ex)];
        }
        catch (DirectoryNotFoundException ex)
        {
            return [MissingFileDiagnostic(text, ex)];
        }
        catch (Exception ex)
        {
            return [new DashSpecDiagnostic(0, 0, 0, 1, ex.Message)];
        }
    }

    /// <summary>Missing asset: keep the loader's own message and point at the file name when the text mentions it.</summary>
    private static DashSpecDiagnostic MissingFileDiagnostic(string text, Exception ex)
    {
        var missingPath = ex is FileNotFoundException fnf && !string.IsNullOrWhiteSpace(fnf.FileName)
            ? fnf.FileName
            : null;

        int? offset = null;
        if (!string.IsNullOrWhiteSpace(missingPath))
        {
            var name = Path.GetFileName(missingPath.TrimEnd('"', '\''));
            if (!string.IsNullOrWhiteSpace(name))
            {
                var idx = text.IndexOf(name, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    offset = idx;
                }
            }
        }

        return TextPositions.ToDiagnostic(text, ex.Message, offset);
    }

    /// <summary>
    /// A `.ds` file is routed by the kind in its module header (ADR-0101); anything else is treated as a document.
    /// </summary>
    private static IReadOnlyList<DashSpecDiagnostic> ValidateByFileKind(
        string text,
        string filePath,
        string specDirectory,
        DashSpecParseOptions parseOptions)
    {
        if (!Path.GetExtension(filePath).Equals(".ds", StringComparison.OrdinalIgnoreCase))
        {
            var document = DashSpecParser.Parse(text, specDirectory, parseOptions);
            return ToDiagnostics(ResolutionLint.Analyze(document));
        }

        var (unitKind, _) = FsharpDocument.UnitSource.tryKindAndId(text);

        switch (unitKind.ToLowerInvariant())
        {
            case "diagram":
                DiagramModuleParser.ParseDiagramFile(text, specDirectory);
                return [];
            case "presentation":
                PresentationModuleParser.ParsePresentationFile(text, specDirectory);
                return [];
            case "palette":
                PaletteModuleParser.ParsePaletteFile(text);
                return [];
            case "transform":
                TransformModuleParser.ParseTransformFile(text);
                return [];
            case "layout":
                LayoutModuleParser.ParseLayoutFile(text);
                return [];
            case "tooltip":
                TooltipModuleParser.ParseTooltipFile(text);
                return [];
            case "types":
            case "flow":
                // Type and flow units have no standalone validation facade: they are validated in the
                // context of the module that imports them.
                return [];
            case "catalog":
                return LintCatalog(text, filePath, specDirectory, parseOptions);
            case "host":
                HostModuleParser.Parse(text, specDirectory);
                return [];
            case "dashboard":
                return ValidateAssembledRoot(text, filePath, specDirectory, parseOptions);
            case "tab":
                return ValidateTabAssembled(text, filePath, specDirectory, parseOptions);
            default:
                var document = DashSpecParser.Parse(text, specDirectory, parseOptions);
                return ToDiagnostics(ResolutionLint.Analyze(document));
        }
    }

    /// <summary>
    /// A root (dashboard) is validated the way the runtime assembles it: tab modules merged in,
    /// then the graph validation over the assembled document. The saved state rides the
    /// assembled-corpus cache; unsaved edits ride the request text (tabs read from disk).
    /// </summary>
    private static IReadOnlyList<DashSpecDiagnostic> ValidateAssembledRoot(
        string text,
        string filePath,
        string specDirectory,
        DashSpecParseOptions parseOptions)
    {
        var corpus = DashSpecCompilerServicesHost.GetCorpus(specDirectory);

        if (corpus.TryGetMemberText(filePath, out var diskText)
            && string.Equals(text, diskText, StringComparison.Ordinal))
        {
            // Saved state: the cached assembled root (one compile per corpus revision).
            if (!corpus.TryGetAssembledRoot(filePath, out var document, out var error, out var errorOffset))
                return [TextPositions.ToDiagnostic(text, error, errorOffset)];

            return GraphDiagnostics(document);
        }

        // Unsaved edits in this buffer: assemble from the request text, merge included.
        try
        {
            var document = DashSpecParser.Parse(text, specDirectory, corpus.AssembledOptions);
            return GraphDiagnostics(document);
        }
        catch (Exception ex) when (ex is not DashSpecParseException)
        {
            return [new DashSpecDiagnostic(0, 0, 0, 1, ex.Message)];
        }
    }

    /// <summary>
    /// A tab is validated the way the runtime runs it: assembled by each importing root.
    /// The tab's slice of the assembled graph validation is attributed through the graph's own
    /// edges — the tab entry (ModulePath) and its card ids. An orphan tab (no importing root)
    /// keeps the standalone composition as the fallback, plus a dead-code warning.
    /// </summary>
    private static IReadOnlyList<DashSpecDiagnostic> ValidateTabAssembled(
        string text,
        string filePath,
        string specDirectory,
        DashSpecParseOptions parseOptions)
    {
        var tabNamespace = DashSpecCompilerServicesHost.OptionValue(FsharpDocument.UnitSource.namespaceOf(text));
        if (string.IsNullOrWhiteSpace(tabNamespace))
        {
            // No namespace: nobody can import it — standalone composition.
            return ValidateStandalone(text, filePath, specDirectory, parseOptions);
        }

        var corpus = DashSpecCompilerServicesHost.GetCorpus(specDirectory);

        // Unsaved edits: the assembled view applies to the saved state; the live text
        // keeps the standalone feedback until flushed.
        if (!corpus.TryGetMemberText(filePath, out var diskText)
            || !string.Equals(text, diskText, StringComparison.Ordinal))
        {
            return ValidateStandalone(text, filePath, specDirectory, parseOptions);
        }

        var roots = corpus.ImportingRoots(tabNamespace);
        if (roots.Count == 0)
        {
            var diagnostics = ValidateStandalone(text, filePath, specDirectory, parseOptions).ToList();
            diagnostics.Add(new DashSpecDiagnostic(
                0, 0, 0, 1,
                $"Tab is not imported by any root — standalone validation only. Import it: import tabs from {tabNamespace} in a dashboard.",
                DashSpecDiagnosticSeverity.Warning));
            return diagnostics;
        }

        var tabPath = Path.GetFullPath(filePath);
        var result = new List<DashSpecDiagnostic>();
        var assembledByAnyRoot = false;

        foreach (var root in roots)
        {
            if (!corpus.TryGetAssembledRoot(root, out var document, out var rootError, out _))
            {
                // The root fails to assemble — the message names the module it concerns.
                result.Add(new DashSpecDiagnostic(0, 0, 0, 1, $"{Path.GetFileName(root)}: {rootError}"));
                continue;
            }

            // The graph's tab→file edge: does this root assemble our file?
            var tabEntry = document.Tabs.FirstOrDefault(tab =>
            {
                var modulePath = DashSpecCompilerServicesHost.OptionValue(tab.ModulePath);
                return modulePath is not null
                    && string.Equals(
                        Path.GetFullPath(modulePath.Trim()),
                        tabPath,
                        StringComparison.OrdinalIgnoreCase);
            });
            if (tabEntry is null)
            {
                // Imports the namespace but resolved to another file (shadow) — not us.
                continue;
            }

            assembledByAnyRoot = true;
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { tabEntry.Id };
            foreach (var cardId in tabEntry.CardIds)
            {
                ids.Add(cardId);
            }

            foreach (var message in DashboardValidationCollector.Collect(document))
            {
                if (MentionsId(message, ids))
                {
                    result.Add(new DashSpecDiagnostic(0, 0, 0, 1, message));
                }
            }

            var tabFindings = ResolutionLint.Analyze(document)
                .Where(finding => MentionsId(finding.FormatMessage(), ids))
                .ToArray();
            result.AddRange(ToDiagnostics(tabFindings));
        }

        if (!assembledByAnyRoot && result.Count == 0)
        {
            // Shadowed everywhere — nobody assembled us; standalone as fallback.
            return ValidateStandalone(text, filePath, specDirectory, parseOptions);
        }

        return result;
    }

    /// <summary>Graph validation over an assembled document: the analyzer chain + resolution lint.</summary>
    private static IReadOnlyList<DashSpecDiagnostic> GraphDiagnostics(FsharpDocument.DashboardDocument document)
    {
        var diagnostics = new List<DashSpecDiagnostic>();
        foreach (var message in DashboardValidationCollector.Collect(document))
        {
            diagnostics.Add(new DashSpecDiagnostic(0, 0, 0, 1, message));
        }

        diagnostics.AddRange(ToDiagnostics(ResolutionLint.Analyze(document)));
        return diagnostics;
    }

    /// <summary>
    /// Standalone composition as the fallback view (orphan tabs, unsaved live text, no
    /// namespace). Self-contained: the standalone parse failures return as diagnostics,
    /// not exceptions — callers may append their own notes to the result.
    /// </summary>
    private static IReadOnlyList<DashSpecDiagnostic> ValidateStandalone(
        string text,
        string filePath,
        string specDirectory,
        DashSpecParseOptions parseOptions)
    {
        try
        {
            var document = DashSpecParser.Parse(text, specDirectory, parseOptions);
            return ToDiagnostics(ResolutionLint.Analyze(document));
        }
        catch (DashSpecParseException ex)
        {
            return [TextPositions.ToDiagnostic(text, ex.Message, ex.SourceOffset)];
        }
        catch (FileNotFoundException ex)
        {
            return [MissingFileDiagnostic(text, ex)];
        }
        catch (DirectoryNotFoundException ex)
        {
            return [MissingFileDiagnostic(text, ex)];
        }
        catch (Exception ex)
        {
            return [new DashSpecDiagnostic(0, 0, 0, 1, ex.Message)];
        }
    }

    /// <summary>Messages quote ids in single quotes ('users', 'app_users') — quoted match avoids prefix hits.</summary>
    private static bool MentionsId(string message, IReadOnlySet<string> ids) =>
        ids.Any(id => message.Contains($"'{id}'", StringComparison.Ordinal));

    private static IReadOnlyList<DashSpecDiagnostic> LintCatalog(
        string text,
        string catalogPath,
        string specDirectory,
        DashSpecParseOptions parseOptions)
    {
        var catalog = CatalogReader.Parse(text);
        var findings = new List<ResolutionLintFinding>();
        foreach (var entry in catalog.Entries)
        {
            var specPath = CatalogReader.ResolveEntrySpecPath(catalogPath, entry.DashspecPath);
            var specDirectoryForEntry = Path.GetDirectoryName(specPath)!;
            var document = DashSpecParser.Parse(File.ReadAllText(specPath), specDirectoryForEntry, parseOptions);
            findings.AddRange(ResolutionLint.Analyze(document, entry));
        }

        return ToDiagnostics(findings);
    }

    private static IReadOnlyList<DashSpecDiagnostic> ToDiagnostics(IReadOnlyList<ResolutionLintFinding> findings)
    {
        if (findings.Count == 0)
        {
            return [];
        }

        return findings
            .Select(finding => new DashSpecDiagnostic(
                0,
                0,
                0,
                1,
                finding.FormatMessage(),
                finding.Severity == ResolutionLintSeverity.Information
                    ? DashSpecDiagnosticSeverity.Information
                    : DashSpecDiagnosticSeverity.Warning))
            .ToList();
    }
}
