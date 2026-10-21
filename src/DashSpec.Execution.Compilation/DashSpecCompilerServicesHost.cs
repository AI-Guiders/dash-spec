using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using DashSpec.Execution.Parsing;
using DashSpec.Execution.Runtime;
using FsharpDocument = DashSpec.Modeling.Parse.Document;
using Microsoft.FSharp.Core;
using DashSpec.Compiler.Contract;

namespace DashSpec.Execution.Compilation;

/// <summary>
/// Assembled-corpus materialization host — the FcsCompilerServicesHost pattern (ADR-0062 family):
/// compile each assembling root once per corpus revision and reuse while no member file changed.
/// The revision stamp is the member files' mtimes (the compiler's dsproj membership);
/// a save or an outside edit bumps an mtime and the next call re-materializes.
/// Per-root documents compile lazily and stay cached until the stamp moves.
/// </summary>
public static class DashSpecCompilerServicesHost
{
    /// <summary>Editor per-file vocabulary with the runtime merge ON — the assembled view.</summary>
    static readonly DashSpecParseOptions AssembledOptions = new()
    {
        MergeReferencedTabModules = true,
        ExtensionBlockKeywords = DashSpecParseOptions.Editor.ExtensionBlockKeywords,
        ExtensionBlockPluginIds = DashSpecParseOptions.Editor.ExtensionBlockPluginIds,
        PhraseTemplates = DashSpecParseOptions.Editor.PhraseTemplates,
        KnownActionHandlers = DashSpecParseOptions.Editor.KnownActionHandlers,
        KnownInteractionHandlers = DashSpecParseOptions.Editor.KnownInteractionHandlers,
        KnownClickSources = DashSpecParseOptions.Editor.KnownClickSources,
    };

    static readonly ConcurrentDictionary<string, DashSpecAssembledCorpus> Corpora = new(StringComparer.OrdinalIgnoreCase);

    public static void Invalidate(string? specDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(specDirectory))
        {
            Corpora.Clear();
            return;
        }

        Corpora.TryRemove(Path.GetFullPath(specDirectory.Trim()), out _);
    }

    /// <summary>Corpus snapshot for a spec directory: materialize once, then reuse while fresh.</summary>
    public static DashSpecAssembledCorpus GetCorpus(string specDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(specDirectory);
        var root = Path.GetFullPath(specDirectory.Trim());

        // Bounded retry: a member changing mid-materialization (or a transiently
        // unreadable member) can make the fresh pass keep failing — serving the
        // last snapshot beats spinning.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var corpus = Corpora.GetOrAdd(root, MaterializeCorpus);
            if (corpus.IsFresh())
                return corpus;

            // A member changed (mtime/new/deleted) — drop the snapshot, rematerialize.
            Corpora.TryRemove(root, out _);
        }

        return Corpora.GetOrAdd(root, MaterializeCorpus);
    }

    static DashSpecAssembledCorpus MaterializeCorpus(string corpusRoot)
    {
        var texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var mtimes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in FsharpDocument.UnitIndexModule.corpusFiles(corpusRoot))
        {
            var full = Path.GetFullPath(file);
            try
            {
                // Stat-read-stat: a member written mid-read would tear the cached text,
                // so only members stable across the read are recorded (unstable members
                // fail the fresh pass and the next materialization re-reads them).
                var before = File.GetLastWriteTimeUtc(full);
                var text = File.ReadAllText(full);
                var after = File.GetLastWriteTimeUtc(full);
                if (before == after)
                {
                    texts[full] = text;
                    mtimes[full] = after;
                }
            }
            catch
            {
                // Stat/read failed (locked or deleted member): not recorded — the
                // fresh pass sees the mismatch and the next materialization retries.
            }
        }

        var roots = new List<string>();
        foreach (var pair in texts)
        {
            // Raw module keyword (tryKindAndId semantics): `dashboard` roots import tab
            // modules; the registry kinds (tab/diagram/flow/…) are imported units.
            var (moduleKind, _) = FsharpDocument.UnitSource.tryKindAndId(pair.Value);
            if (string.Equals(moduleKind, "dashboard", StringComparison.OrdinalIgnoreCase))
                roots.Add(pair.Key);
        }

        return new DashSpecAssembledCorpus(corpusRoot, texts, mtimes, roots, AssembledOptions);
    }

    internal static string? OptionValue(FSharpOption<string>? option) =>
        option is not null && FSharpOption<string>.get_IsSome(option) ? option.Value : null;
}

/// <summary>
/// One assembled-corpus snapshot: member texts and mtimes at materialization, the assembling
/// roots (dashboard units), and the lazy per-root assembled-document cache. Immutable once
/// built; stale snapshots are replaced wholesale by the host.
/// </summary>
public sealed class DashSpecAssembledCorpus(
    string corpusRoot,
    IReadOnlyDictionary<string, string> memberTexts,
    IReadOnlyDictionary<string, DateTime> memberMtimes,
    IReadOnlyList<string> rootFiles,
    DashSpecParseOptions assembledOptions)
{
    static readonly Regex ImportTabsLine = new(
        @"(?m)^\s*import\s+tabs\s+from\s+([A-Za-z_][\w.]*)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    readonly object _gate = new();
    readonly Dictionary<string, (FsharpDocument.DashboardDocument? Document, string? Error, int? ErrorOffset)> _assembled =
        new(StringComparer.OrdinalIgnoreCase);

    public string CorpusRoot { get; } = corpusRoot;

    public IReadOnlyList<string> RootFiles { get; } = rootFiles;

    public DashSpecParseOptions AssembledOptions { get; } = assembledOptions;

    /// <summary>The corpus member text as materialized (the saved state of the corpus).</summary>
    public bool TryGetMemberText(string path, [NotNullWhen(true)] out string? text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return memberTexts.TryGetValue(Path.GetFullPath(path.Trim()), out text);
    }

    /// <summary>Fresh pass: the current membership must be the same set with the same mtimes.</summary>
    public bool IsFresh()
    {
        var files = FsharpDocument.UnitIndexModule.corpusFiles(CorpusRoot);
        if (files.Length != memberMtimes.Count)
            return false;

        foreach (var file in files)
        {
            var full = Path.GetFullPath(file);
            if (!memberMtimes.TryGetValue(full, out var recorded))
                return false;

            try
            {
                if (File.GetLastWriteTimeUtc(full) != recorded)
                    return false;
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Roots whose text imports <c>tabs from &lt;tabNamespace&gt;</c> (the compiler's import grammar).</summary>
    public IReadOnlyList<string> ImportingRoots(string tabNamespace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabNamespace);
        var importing = new List<string>();
        foreach (var root in RootFiles)
        {
            if (!memberTexts.TryGetValue(root, out var text))
                continue;

            foreach (Match match in ImportTabsLine.Matches(text))
            {
                if (string.Equals(match.Groups[1].Value, tabNamespace, StringComparison.OrdinalIgnoreCase))
                {
                    importing.Add(root);
                    break;
                }
            }
        }

        return importing;
    }

    /// <summary>
    /// Assemble a root (lazily, cached for the snapshot lifetime). The graph's own tab→file edge
    /// (TabDefinition.ModulePath) confirms which roots actually assemble a given tab file.
    /// Returns false with <paramref name="error"/> when the assembled parse fails.
    /// </summary>
    public bool TryGetAssembledRoot(
        string rootPath,
        [NotNullWhen(true)] out FsharpDocument.DashboardDocument? document,
        [NotNullWhen(false)] out string? error,
        out int? errorOffset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        var key = Path.GetFullPath(rootPath.Trim());

        lock (_gate)
        {
            if (_assembled.TryGetValue(key, out var cached))
            {
                document = cached.Document;
                error = cached.Error;
                errorOffset = cached.ErrorOffset;
                return document is not null;
            }
        }

        (FsharpDocument.DashboardDocument? Document, string? Error, int? ErrorOffset) assembled;
        try
        {
            if (!memberTexts.TryGetValue(key, out var rootText))
                throw new FileNotFoundException("Corpus member not found.", key);

            var parsed = DashSpecParser.Parse(
                rootText,
                Path.GetDirectoryName(key),
                AssembledOptions);
            assembled = (parsed, null, null);
        }
        catch (DashSpecParseException ex)
        {
            assembled = (null, ex.Message, ex.SourceOffset);
        }
        catch (Exception ex)
        {
            assembled = (null, ex.Message, null);
        }

        lock (_gate)
        {
            _assembled[key] = assembled;
        }

        document = assembled.Document;
        error = assembled.Error;
        errorOffset = assembled.ErrorOffset;
        return document is not null;
    }
}
