#nullable enable

using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using AIGuiders.Platform.Modeling.CodeCenter;
using AIGuiders.Platform.Modeling.Language;
using DashSpec.Abstractions.Hosting;
using DashSpec.Abstractions.Viewer;
using DashSpec.CodeCenter;
using DashSpec.Compiler.Contract;
using DashSpec.Composition.Plugins;
using DashSpec.Execution.Compilation;
using DashSpec.Execution.Parsing;
using DashSpec.Execution.Resolution;
using DashSpec.Modeling.Core;
using DashSpec.Modeling.Language.Adapters.DashSpec;
using DashSpec.Modeling.Parse.DataFlow;
using DashSpec.Modeling.Parse.Document;
using DashSpec.Modeling.Parse.Types;

namespace DashSpec.Plugin.Studio.Services;

public sealed record StudioDraftDocument(
    string RelativePath,
    string BaseContentHash,
    string BaseText,
    DocumentSnapshot Snapshot,
    DashSpecConceptGraph ConceptGraph,
    DateTimeOffset ModifiedAtUtc);

public sealed record StudioDocumentInspection(
    string RelativePath,
    DashSpecUnitDeclaration? Unit,
    string Text,
    string DiskText,
    string DiskHash,
    string BaseHash,
    bool IsDraftModified,
    bool HasUpstreamConflict,
    DocumentSnapshot Snapshot,
    DashSpecConceptGraph ConceptGraph,
    IReadOnlyList<LanguageDiagnostic> FileDiagnostics,
    IReadOnlyList<LanguageDiagnostic> AssemblyHints,
    WiringGraph? WiringGraph,
    FlowGraph? FlowGraph,
    ResolvedSpecExport? ResolvedReport,
    string? ResolveHint);

public sealed record StudioPublishOutcome(
    bool Success,
    string Message,
    IReadOnlyList<string> PublishedFiles,
    IReadOnlyList<string> ConflictFiles);

/// <summary>
/// Manages the published catalog unit index (<see cref="DashSpecWorkspaceIndex"/>),
/// per-author isolated <see cref="DocumentSnapshot"/> Draft Overlays (<see cref="DashSpecDocumentGraph"/>),
/// <see cref="DashSpecLanguageBackend"/> LRC operations, typed <see cref="WiringGraph"/> / <see cref="FlowGraph"/> projections,
/// and atomic publish with SHA-256 optimistic concurrency (ADR-0133).
/// </summary>
public sealed class StudioWorkspaceService(
    IViewerRuntimeContext runtimeContext,
    DashSpecParseOptionsProvider parseOptionsProvider,
    IReportCompiler reportCompiler,
    IDevSpecReloadSignal reloadSignal,
    StudioPresenceTracker presenceTracker,
    StudioGitService gitService)
{
    private readonly ILanguageBackend _languageBackend = new DashSpecLanguageBackend();

    public ILanguageBackend LanguageBackend => _languageBackend;

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, StudioDraftDocument>> _authorDrafts =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly SemaphoreSlim _publishLock = new(1, 1);

    public string ResolveWorkspaceRoot()
    {
        var catalogPath = runtimeContext.CatalogFullPath;
        if (!string.IsNullOrWhiteSpace(catalogPath) && File.Exists(catalogPath))
        {
            return DashSpec.Modeling.Parse.Project.DashSpecProjectLocator.resolveProjectRoot(catalogPath);
        }

        if (!string.IsNullOrWhiteSpace(runtimeContext.DefaultSpecDirectory) &&
            Directory.Exists(runtimeContext.DefaultSpecDirectory))
        {
            return DashSpec.Modeling.Parse.Project.DashSpecProjectLocator.resolveProjectRoot(runtimeContext.DefaultSpecDirectory);
        }

        return AppContext.BaseDirectory;
    }

    public DashSpecWorkspaceIndex GetWorkspaceIndex() =>
        DashSpecCompilerServicesHost.GetCorpus(ResolveWorkspaceRoot()).GetWorkspaceIndex();

    public string ToRelativePath(string fullPath) =>
        Path.GetRelativePath(ResolveWorkspaceRoot(), fullPath).Replace('\\', '/');

    public IReadOnlyDictionary<string, StudioDraftDocument> GetAuthorDrafts(string authorId)
    {
        if (_authorDrafts.TryGetValue(authorId, out var drafts))
        {
            return drafts;
        }

        return new Dictionary<string, StudioDraftDocument>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<StudioLspDiagnosticDto>> EvaluateDiagnosticsOnlyAsync(
        string relativePath,
        string text,
        CancellationToken ct = default)
    {
        var root = ResolveWorkspaceRoot();
        var normalizedRel = NormalizeRelativePath(relativePath);
        var fullPath = Path.GetFullPath(Path.Combine(root, normalizedRel));

        var langReq = new LanguageRequest(
            fullPath,
            1,
            1,
            text ?? string.Empty,
            root,
            LanguageIds.Dashspec);

        var diagsResult = await _languageBackend.GetDiagnosticsAsync(langReq, ct);
        return diagsResult.Diagnostics
            .Where(d => d.Severity == Severity.Error || d.Span.Line > 1 || d.Span.Column > 1)
            .Select(d => new StudioLspDiagnosticDto(
                Math.Max(1, d.Span.Line),
                Math.Max(1, d.Span.Column),
                Math.Max(1, d.Span.EndLine),
                Math.Max(2, d.Span.EndColumn),
                d.Message,
                d.Severity == Severity.Error ? "error" : d.Severity == Severity.Warning ? "warning" : "info",
                d.Id))
            .ToList();
    }

    public async Task<StudioDocumentInspection> InspectDocumentAsync(
        string authorId,
        string authorName,
        string relativePath,
        CancellationToken ct = default)
    {
        var root = ResolveWorkspaceRoot();
        var normalizedRel = NormalizeRelativePath(relativePath);
        var fullPath = Path.GetFullPath(Path.Combine(root, normalizedRel));

        var diskText = File.Exists(fullPath) ? SafeReadAllText(fullPath) : string.Empty;
        var diskHash = ComputeHash(diskText);

        var drafts = _authorDrafts.GetOrAdd(
            authorId,
            _ => new ConcurrentDictionary<string, StudioDraftDocument>(StringComparer.OrdinalIgnoreCase));

        DocumentSnapshot snapshot;
        DashSpecConceptGraph conceptGraph;
        string baseHash;
        bool isDraftModified;

        if (drafts.TryGetValue(normalizedRel, out var draft))
        {
            snapshot = draft.Snapshot;
            conceptGraph = draft.ConceptGraph;
            baseHash = draft.BaseContentHash;
            isDraftModified = !string.Equals(snapshot.Text, draft.BaseText, StringComparison.Ordinal);
        }
        else
        {
            (snapshot, conceptGraph, _) = DashSpecDocumentGraph.rebuildWithConceptGraph(diskText);
            baseHash = diskHash;
            isDraftModified = false;
        }

        var activeText = snapshot.Text;
        var hasUpstreamConflict = isDraftModified && !string.Equals(baseHash, diskHash, StringComparison.Ordinal);
        presenceTracker.UpdateAuthor(authorId, authorName, normalizedRel, drafts.Keys);

        var index = GetWorkspaceIndex();
        var unit = index.Units.FirstOrDefault(u =>
            string.Equals(ToRelativePath(u.Path), normalizedRel, StringComparison.OrdinalIgnoreCase));

        var langReq = new LanguageRequest(
            fullPath,
            1,
            1,
            activeText,
            root,
            LanguageIds.Dashspec);

        var diagsResult = await _languageBackend.GetDiagnosticsAsync(langReq, ct);

        // Separate file-local errors/warnings (with actual line numbers or Error severity)
        // from cross-section ResolutionLint notes (Line == 1 && Column == 1 && Severity != Error)
        var fileDiagnostics = diagsResult.Diagnostics
            .Where(d => d.Severity == Severity.Error || d.Span.Line > 1 || d.Span.Column > 1)
            .ToList();
        var assemblyHints = diagsResult.Diagnostics
            .Where(d => d.Severity != Severity.Error && d.Span.Line <= 1 && d.Span.Column <= 1)
            .ToList();

        var (wiringGraph, flowGraph, resolvedReport, resolveHint) =
            BuildTypedGraphsAndPreview(fullPath, activeText, unit, index);

        return new StudioDocumentInspection(
            normalizedRel,
            unit,
            activeText,
            diskText,
            diskHash,
            baseHash,
            isDraftModified,
            hasUpstreamConflict,
            snapshot,
            conceptGraph,
            fileDiagnostics,
            assemblyHints,
            wiringGraph,
            flowGraph,
            resolvedReport,
            resolveHint);
    }

    public async Task<StudioDocumentInspection> UpdateDraftAsync(
        string authorId,
        string authorName,
        string relativePath,
        string newText,
        CancellationToken ct = default)
    {
        var root = ResolveWorkspaceRoot();
        var normalizedRel = NormalizeRelativePath(relativePath);
        var fullPath = Path.GetFullPath(Path.Combine(root, normalizedRel));
        var diskText = File.Exists(fullPath) ? SafeReadAllText(fullPath) : string.Empty;
        var diskHash = ComputeHash(diskText);

        var drafts = _authorDrafts.GetOrAdd(
            authorId,
            _ => new ConcurrentDictionary<string, StudioDraftDocument>(StringComparer.OrdinalIgnoreCase));

        if (string.Equals(newText, diskText, StringComparison.Ordinal))
        {
            drafts.TryRemove(normalizedRel, out _);
        }
        else
        {
            var (rebuiltSnapshot, rebuiltConceptGraph, _) = DashSpecDocumentGraph.rebuildWithConceptGraph(newText);
            drafts.AddOrUpdate(
                normalizedRel,
                _ => new StudioDraftDocument(
                    normalizedRel,
                    diskHash,
                    diskText,
                    rebuiltSnapshot,
                    rebuiltConceptGraph,
                    DateTimeOffset.UtcNow),
                (_, existing) => existing with
                {
                    Snapshot = rebuiltSnapshot,
                    ConceptGraph = rebuiltConceptGraph,
                    ModifiedAtUtc = DateTimeOffset.UtcNow,
                });
        }

        return await InspectDocumentAsync(authorId, authorName, normalizedRel, ct);
    }

    public async Task<StudioDocumentInspection> ApplyStructuralEditAsync(
        string authorId,
        string authorName,
        string relativePath,
        StructuralEdit edit,
        CancellationToken ct = default)
    {
        var root = ResolveWorkspaceRoot();
        var normalizedRel = NormalizeRelativePath(relativePath);
        var fullPath = Path.GetFullPath(Path.Combine(root, normalizedRel));
        var diskText = File.Exists(fullPath) ? SafeReadAllText(fullPath) : string.Empty;
        var diskHash = ComputeHash(diskText);

        var drafts = _authorDrafts.GetOrAdd(
            authorId,
            _ => new ConcurrentDictionary<string, StudioDraftDocument>(StringComparer.OrdinalIgnoreCase));

        var draft = drafts.GetOrAdd(
            normalizedRel,
            _ =>
            {
                var (snap, cg, _) = DashSpecDocumentGraph.rebuildWithConceptGraph(diskText);
                return new StudioDraftDocument(
                    normalizedRel,
                    diskHash,
                    diskText,
                    snap,
                    cg,
                    DateTimeOffset.UtcNow);
            });

        var outcome = DashSpecStructuralPlanner.plan(draft.Snapshot, edit);
        if (outcome.IsOk)
        {
            var (_, newCg, _) = DashSpecDocumentGraph.rebuildWithConceptGraph(outcome.ResultValue.Snapshot.Text);
            drafts[normalizedRel] = draft with
            {
                Snapshot = outcome.ResultValue.Snapshot,
                ConceptGraph = newCg,
                ModifiedAtUtc = DateTimeOffset.UtcNow,
            };
        }

        return await InspectDocumentAsync(authorId, authorName, normalizedRel, ct);
    }

    public async Task<StudioDocumentInspection> FormatDraftAsync(
        string authorId,
        string authorName,
        string relativePath,
        string currentText,
        CancellationToken ct = default)
    {
        var (graph, _) = DashSpecSerializeRules.parseAndBuild(currentText);
        var formatted = DashSpecSerializeRules.serializeFormat(graph);
        return await UpdateDraftAsync(authorId, authorName, relativePath, formatted, ct);
    }

    public async Task<StudioDocumentInspection> DiscardDraftAsync(
        string authorId,
        string authorName,
        string relativePath,
        CancellationToken ct = default)
    {
        var normalizedRel = NormalizeRelativePath(relativePath);
        if (_authorDrafts.TryGetValue(authorId, out var drafts))
        {
            drafts.TryRemove(normalizedRel, out _);
        }

        return await InspectDocumentAsync(authorId, authorName, normalizedRel, ct);
    }

    public void DiscardAllDrafts(string authorId, string authorName, string? activeRelativePath)
    {
        if (_authorDrafts.TryGetValue(authorId, out var drafts))
        {
            drafts.Clear();
        }

        presenceTracker.UpdateAuthor(authorId, authorName, activeRelativePath, []);
    }

    public async Task<StudioDocumentInspection> RebaseDraftOntoDiskAsync(
        string authorId,
        string authorName,
        string relativePath,
        CancellationToken ct = default)
    {
        var root = ResolveWorkspaceRoot();
        var normalizedRel = NormalizeRelativePath(relativePath);
        var fullPath = Path.GetFullPath(Path.Combine(root, normalizedRel));
        var diskText = File.Exists(fullPath) ? SafeReadAllText(fullPath) : string.Empty;
        var diskHash = ComputeHash(diskText);

        if (_authorDrafts.TryGetValue(authorId, out var drafts) &&
            drafts.TryGetValue(normalizedRel, out var existing))
        {
            drafts[normalizedRel] = existing with
            {
                BaseContentHash = diskHash,
                BaseText = diskText,
            };
        }

        return await InspectDocumentAsync(authorId, authorName, normalizedRel, ct);
    }

    public async Task<StudioPublishOutcome> PublishAuthorDraftsAsync(
        string authorId,
        string authorName,
        bool commitToGit,
        string? commitMessage,
        bool forceOverwriteConflicts = false,
        CancellationToken ct = default)
    {
        if (!_authorDrafts.TryGetValue(authorId, out var drafts) || drafts.IsEmpty)
        {
            return new StudioPublishOutcome(false, "В черновике нет изменённых файлов для публикации.", [], []);
        }

        await _publishLock.WaitAsync(ct);
        try
        {
            var root = ResolveWorkspaceRoot();
            var toPublish = drafts.Values
                .Where(d => !string.Equals(d.Snapshot.Text, d.BaseText, StringComparison.Ordinal))
                .OrderBy(d => d.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (toPublish.Count == 0)
            {
                drafts.Clear();
                return new StudioPublishOutcome(false, "Нет отличий от базовых версий файлов.", [], []);
            }

            foreach (var item in toPublish)
            {
                var fullPath = Path.GetFullPath(Path.Combine(root, item.RelativePath));
                var req = new LanguageRequest(
                    fullPath,
                    1,
                    1,
                    item.Snapshot.Text,
                    root,
                    LanguageIds.Dashspec);
                var diags = await _languageBackend.GetDiagnosticsAsync(req, ct);
                var errors = diags.Diagnostics.Where(d => d.Severity == Severity.Error).ToList();
                if (errors.Count > 0)
                {
                    var first = errors[0];
                    return new StudioPublishOutcome(
                        false,
                        $"Публикация отклонена: ошибка компиляции в '{item.RelativePath}' (L{first.Span.Line}): {first.Message}",
                        [],
                        []);
                }
            }

            var conflicts = new List<string>();
            foreach (var item in toPublish)
            {
                var fullPath = Path.GetFullPath(Path.Combine(root, item.RelativePath));
                var diskText = File.Exists(fullPath) ? SafeReadAllText(fullPath) : string.Empty;
                var diskHash = ComputeHash(diskText);
                if (!forceOverwriteConflicts &&
                    !string.Equals(item.BaseContentHash, diskHash, StringComparison.Ordinal))
                {
                    conflicts.Add(item.RelativePath);
                }
            }

            if (conflicts.Count > 0)
            {
                return new StudioPublishOutcome(
                    false,
                    $"Обнаружен конфликт версий: файлы [{string.Join(", ", conflicts)}] были изменены на сервере другим автором.",
                    [],
                    conflicts);
            }

            var publishedPaths = new List<string>();
            foreach (var item in toPublish)
            {
                var fullPath = Path.GetFullPath(Path.Combine(root, item.RelativePath));
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                await File.WriteAllTextAsync(fullPath, item.Snapshot.Text, Encoding.UTF8, ct);
                publishedPaths.Add(item.RelativePath);
            }

            DashSpecCompilerServicesHost.Invalidate(root);
            drafts.Clear();
            presenceTracker.UpdateAuthor(authorId, authorName, publishedPaths.FirstOrDefault(), []);

            var gitNote = string.Empty;
            if (commitToGit)
            {
                var (gitOk, gitMsg) = gitService.CommitFiles(
                    root,
                    publishedPaths,
                    authorName,
                    commitMessage ?? $"studio: update {string.Join(", ", publishedPaths)}");
                gitNote = gitOk ? $" Git commit: {gitMsg}" : $" (Файлы записаны, но git commit сообщил: {gitMsg})";
            }

            reloadSignal.Notify();

            return new StudioPublishOutcome(
                true,
                $"Опубликовано файлов: {publishedPaths.Count}.{gitNote}",
                publishedPaths,
                []);
        }
        finally
        {
            _publishLock.Release();
        }
    }

    /// <summary>
    /// Renders syntax-highlighted HTML directly from <see cref="DocumentSnapshot.TokenSpans"/>.
    /// </summary>
    public static string RenderHighlightedHtml(DocumentSnapshot snapshot)
    {
        var text = snapshot.Text;
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var spans = snapshot.TokenSpans
            .Where(s => s.Start >= 0 && s.Start < text.Length && s.Length > 0)
            .OrderBy(s => s.Start)
            .ToList();

        var sb = new StringBuilder(text.Length * 2);
        var pos = 0;

        foreach (var span in spans)
        {
            if (span.Start < pos)
            {
                continue;
            }

            if (span.Start > pos)
            {
                sb.Append(WebUtility.HtmlEncode(text[pos..span.Start]));
            }

            var end = Math.Min(text.Length, span.Start + span.Length);
            var tokenText = WebUtility.HtmlEncode(text[span.Start..end]);
            var css = $"ds-tok-{span.Kind.ToLowerInvariant()}";
            sb.Append("<span class=\"").Append(css).Append("\">").Append(tokenText).Append("</span>");
            pos = end;
        }

        if (pos < text.Length)
        {
            sb.Append(WebUtility.HtmlEncode(text[pos..]));
        }

        if (text.EndsWith('\n'))
        {
            sb.Append(' ');
        }

        return sb.ToString();
    }

    private (
        WiringGraph? Wiring,
        FlowGraph? Flow,
        ResolvedSpecExport? Export,
        string? Hint)
        BuildTypedGraphsAndPreview(
            string fullPath,
            string text,
            DashSpecUnitDeclaration? unit,
            DashSpecWorkspaceIndex index)
    {
        var kind = unit?.Kind ?? "";

        if (kind.Equals("flow", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var corpus = DashSpecCompilerServicesHost.GetCorpus(ResolveWorkspaceRoot());
                var typeCatalog = corpus.GetTypeCatalog();
                var flowModule = DashflowModuleParser.parseModuleWithDefaults(
                    text,
                    typeCatalog,
                    Microsoft.FSharp.Core.FSharpOption<DashflowProviderBinding>.Some(DashflowProviderBinding.Infer),
                    "utc");
                return (null, flowModule.Graph, null, $"Поток данных '{flowModule.FlowId}': узлов {flowModule.Graph.Nodes.Count}, связей {flowModule.Graph.Edges.Length}.");
            }
            catch (Exception ex)
            {
                return (null, null, null, $"Ошибка построения FlowGraph: {ex.Message}");
            }
        }

        if (kind.Equals("dashboard", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("section", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("tab", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("report", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var runtimeOptions = parseOptionsProvider.CreateOptions();
                var parseOptions = new DashSpecParseOptions
                {
                    MergeReferencedTabModules = kind.Equals("dashboard", StringComparison.OrdinalIgnoreCase) ||
                                                kind.Equals("report", StringComparison.OrdinalIgnoreCase),
                    ExtensionBlockKeywords = runtimeOptions.ExtensionBlockKeywords,
                    ExtensionBlockPluginIds = runtimeOptions.ExtensionBlockPluginIds,
                    PhraseTemplates = runtimeOptions.PhraseTemplates,
                    KnownActionHandlers = runtimeOptions.KnownActionHandlers,
                    KnownInteractionHandlers = runtimeOptions.KnownInteractionHandlers,
                    KnownClickSources = runtimeOptions.KnownClickSources,
                };

                var specDir = Path.GetDirectoryName(fullPath);
                var document = reportCompiler.Compile(text, specDir, parseOptions).Document;
                var library = SpecLibraryComposer.Load(
                    fullPath,
                    document.DiagramLibraryPath?.Value,
                    ResolveWorkspaceRoot(),
                    document);
                var export = SpecResolveExporter.Export(document, library);
                return (document.WiringGraph, null, export, null);
            }
            catch (Exception ex)
            {
                return (null, null, null, $"Не удалось построить WiringGraph: {ex.Message}");
            }
        }

        return (null, null, null, $"Модуль 'module {unit?.Kind ?? "unit"} {unit?.Id ?? ""}' — декларативный юнит (граф проводки строится для dashboard / section / flow).");
    }

    private static string NormalizeRelativePath(string relativePath) =>
        relativePath.Trim().TrimStart('/', '\\').Replace('\\', '/');

    private static string SafeReadAllText(string fullPath)
    {
        using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string ComputeHash(string content)
    {
        var normalized = content.Replace("\r\n", "\n");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}
