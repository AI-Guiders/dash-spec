using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DashSpec.Compiler.Contract;
using DashSpec.Execution.Parsing;
using DashSpec.Execution.Resolution;
using DashSpec.Execution.Runtime;
using DashSpec.Modeling.Parse.Layout;

namespace DashSpec.Execution.Compilation;

/// <summary>
/// ADR-0136: Precompiled report bundle manifest (<c>DashSpec.ReportBundle.json</c>) and
/// in-memory precompiled graph cache (<see cref="DashboardDocument"/> + <see cref="SpecLibrary"/>
/// + <see cref="SlotPlane"/>) validated by SHA-256 corpus content hashes.
/// </summary>
public static class PrecompiledReportBundle
{
    public const string BundleFileName = "DashSpec.ReportBundle.json";
    public const int FormatVersion = 1;

    private static readonly ConcurrentDictionary<string, CachedCompiledReport> MemoryCache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public sealed record BundleFileEntry(
        string RelativePath,
        string Sha256,
        long Length);

    public sealed record BundleReportEntry(
        string EntryId,
        string RelativeSpecPath,
        string SpecSha256,
        string? RuntimePath,
        string? ConnectorId,
        int TabCount,
        int CardCount,
        int FilterCount);

    public sealed record BundleManifest(
        int Version,
        string CorpusHash,
        DateTimeOffset BuiltAtUtc,
        IReadOnlyList<BundleFileEntry> Files,
        IReadOnlyList<BundleReportEntry> Reports);

    public sealed record BuildBundleResult(
        string BundlePath,
        string CorpusHash,
        int FileCount,
        int ReportCount,
        IReadOnlyList<string> ReportEntryIds);

    public sealed record CachedCompiledReport(
        string SpecFullPath,
        string CorpusHash,
        string SpecHash,
        string EntryRuntimePath,
        DashboardDocument Document,
        SpecLibrary? Library,
        SlotPlane SlotPlane);

    public static BuildBundleResult BuildCorpusBundle(
        string corpusDirectory,
        string? outputDirectory = null,
        IReportCompiler? compiler = null,
        DashSpecParseOptions? parseOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(corpusDirectory);
        var fullCorpusDir = Path.GetFullPath(corpusDirectory);
        if (!Directory.Exists(fullCorpusDir))
        {
            throw new DirectoryNotFoundException($"Corpus directory not found: {fullCorpusDir}");
        }

        compiler ??= new ReportCompiler();
        parseOptions ??= DashSpecParseOptions.Default;

        var files = CollectCorpusFiles(fullCorpusDir);
        var corpusHash = ComputeCorpusHash(files);

        var reports = new List<BundleReportEntry>();
        DashSpecParser.EnsureModuleParsersRegistered();
        var unitIndex = DashSpec.Modeling.Parse.Document.UnitIndexModule.build(fullCorpusDir);

        var seenReportPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var catEntry in unitIndex.Entries.Where(e =>
                     e.Kind is not null &&
                     string.Equals(e.Kind.Value, "catalog", StringComparison.OrdinalIgnoreCase) &&
                     File.Exists(e.Path)))
        {
            var catDoc = compiler.ParseCatalogFile(catEntry.Path);
            var catDir = Path.GetDirectoryName(Path.GetFullPath(catEntry.Path))!;
            foreach (var item in catDoc.Entries)
            {
                if (string.IsNullOrWhiteSpace(item.DashspecPath))
                {
                    continue;
                }

                    var specFullPath = CatalogReader.ResolveEntrySpecPath(catEntry.Path, item.DashspecPath);
                    if (File.Exists(specFullPath) && seenReportPaths.Add(specFullPath))
                    {
                        CompileOneReport(specFullPath, item.Id, fullCorpusDir, corpusHash, compiler, parseOptions, reports);
                    }
            }
        }

        foreach (var entry in unitIndex.Entries)
        {
            if (entry.Kind is null || !File.Exists(entry.Path))
            {
                continue;
            }

            if (!string.Equals(entry.Kind.Value, "report", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(entry.Kind.Value, "dashboard", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var specFullPath = Path.GetFullPath(entry.Path);
            if (!seenReportPaths.Add(specFullPath))
            {
                continue;
            }

            var entryId = entry.ModuleId?.Value ?? Path.GetFileNameWithoutExtension(specFullPath);
            CompileOneReport(specFullPath, entryId, fullCorpusDir, corpusHash, compiler, parseOptions, reports);
        }

        var targetDir = !string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.GetFullPath(outputDirectory)
            : ResolveDefaultBundleDirectory(fullCorpusDir);
        Directory.CreateDirectory(targetDir);

        var bundlePath = Path.Combine(targetDir, BundleFileName);
        var manifest = new BundleManifest(
            FormatVersion,
            corpusHash,
            DateTimeOffset.UtcNow,
            files,
            reports);

        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        File.WriteAllText(bundlePath, json, Encoding.UTF8);

        return new BuildBundleResult(
            bundlePath,
            corpusHash,
            files.Count,
            reports.Count,
            reports.Select(r => r.EntryId).ToList());
    }

    public static bool TryGetOrCompile(
        string text,
        string specFullPath,
        string defaultSpecDirectory,
        IReportCompiler reportCompiler,
        Func<DashSpecParseOptions> parseOptionsFactory,
        out CachedCompiledReport compiled)
    {
        var normalizedPath = Path.GetFullPath(specFullPath);
        var specHash = ComputeTextSha256(text);
        var rootDir = !string.IsNullOrWhiteSpace(defaultSpecDirectory) && Directory.Exists(defaultSpecDirectory)
            ? Path.GetFullPath(defaultSpecDirectory)
            : Path.GetDirectoryName(normalizedPath) ?? AppContext.BaseDirectory;

        var currentCorpusHash = ComputeDirectoryQuickHash(rootDir);

        if (MemoryCache.TryGetValue(normalizedPath, out var existing) &&
            string.Equals(existing.SpecHash, specHash, StringComparison.Ordinal) &&
            string.Equals(existing.CorpusHash, currentCorpusHash, StringComparison.Ordinal))
        {
            compiled = existing;
            return true;
        }

        var entryRuntime = reportCompiler.ReadRuntimePath(text);
        if (string.IsNullOrWhiteSpace(entryRuntime))
        {
            compiled = null!;
            return false;
        }

        var specDirectory = Path.GetDirectoryName(normalizedPath);
        var compileResult = reportCompiler.Compile(text, specDirectory, parseOptionsFactory());
        var document = compileResult.Document;
        var library = SpecLibraryComposer.Load(
            normalizedPath,
            document.DiagramLibraryPath?.Value,
            defaultSpecDirectory,
            document);
        _ = SpecResolver.Resolve(document, library);
        var slotPlane = SlotPlaneBuilder.Build(document, library);

        compiled = new CachedCompiledReport(
            normalizedPath,
            currentCorpusHash,
            specHash,
            entryRuntime,
            document,
            library,
            slotPlane);
        MemoryCache[normalizedPath] = compiled;
        return true;
    }

    public static void InvalidateAll() => MemoryCache.Clear();

    private static void CompileOneReport(
        string specFullPath,
        string entryId,
        string fullCorpusDir,
        string corpusHash,
        IReportCompiler compiler,
        DashSpecParseOptions parseOptions,
        List<BundleReportEntry> reports)
    {
        var text = File.ReadAllText(specFullPath);
        var runtimePath = compiler.ReadRuntimePath(text);
        if (string.IsNullOrWhiteSpace(runtimePath))
        {
            return;
        }

        var specSha = ComputeTextSha256(text);
        var specDir = Path.GetDirectoryName(specFullPath);
        var compiled = compiler.Compile(text, specDir, parseOptions);
        var doc = compiled.Document;
        var library = SpecLibraryComposer.Load(
            specFullPath,
            doc.DiagramLibraryPath?.Value,
            fullCorpusDir,
            doc);
        _ = SpecResolver.Resolve(doc, library);
        var slotPlane = SlotPlaneBuilder.Build(doc, library);

        MemoryCache[specFullPath] = new CachedCompiledReport(
            specFullPath,
            corpusHash,
            specSha,
            runtimePath,
            doc,
            library,
            slotPlane);

        var relPath = Path.GetRelativePath(fullCorpusDir, specFullPath).Replace('\\', '/');
        reports.Add(new BundleReportEntry(
            entryId,
            relPath,
            specSha,
            runtimePath,
            doc.ConnectorId?.Value,
            doc.Tabs.Count,
            doc.Cards.Count,
            doc.Filters.Count));
    }

    private static string ResolveDefaultBundleDirectory(string fullCorpusDir)
    {
        var catalogsDir = Path.Combine(fullCorpusDir, "catalogs");
        return Directory.Exists(catalogsDir) ? catalogsDir : fullCorpusDir;
    }

    private static IReadOnlyList<BundleFileEntry> CollectCorpusFiles(string rootDirectory)
    {
        var list = new List<BundleFileEntry>();
        foreach (var file in Directory.EnumerateFiles(rootDirectory, "*.*", SearchOption.AllDirectories)
                     .Where(f => f.EndsWith(".ds", StringComparison.OrdinalIgnoreCase)
                                 || f.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
                                 || f.EndsWith(".dsproj", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var bytes = File.ReadAllBytes(file);
            var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var rel = Path.GetRelativePath(rootDirectory, file).Replace('\\', '/');
            list.Add(new BundleFileEntry(rel, sha, bytes.LongLength));
        }

        return list;
    }

    private static string ComputeCorpusHash(IReadOnlyList<BundleFileEntry> files)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in files)
        {
            sha.AppendData(Encoding.UTF8.GetBytes(file.RelativePath));
            sha.AppendData(Encoding.UTF8.GetBytes(":"));
            sha.AppendData(Encoding.UTF8.GetBytes(file.Sha256));
            sha.AppendData(Encoding.UTF8.GetBytes("\n"));
        }

        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }

    private static string ComputeDirectoryQuickHash(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return string.Empty;
        }

        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in Directory.EnumerateFiles(rootDirectory, "*.ds", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var info = new FileInfo(file);
            var line = $"{file}:{info.Length}:{info.LastWriteTimeUtc.Ticks}\n";
            sha.AppendData(Encoding.UTF8.GetBytes(line));
        }

        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }

    private static string ComputeTextSha256(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
