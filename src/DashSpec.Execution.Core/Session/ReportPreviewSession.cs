using System.Security.Cryptography;
using System.Text;
using DashSpec.Abstractions.Session;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;
using DashSpec.Execution.Resolution;
using DashSpec.Execution.Runtime;

namespace DashSpec.Execution.Session;

/// <summary>
/// Headless preview session for Studio Report Preview and Host parity tests
/// (<see href="design/DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md">ADR-0047</see>).
/// </summary>
public sealed class ReportPreviewSession : IReportPreviewSession
{
    private DashboardDocument? _document;
    private ResolvedDashboard? _resolved;
    private FilterState? _filters;
    private DateOnly _previewDate = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Parsed dashboard document after <see cref="LoadSpecAsync"/>.</summary>
    public DashboardDocument Document =>
        _document ?? throw new InvalidOperationException("Dashboard spec is not loaded.");

    /// <summary>Resolved cards and library after <see cref="LoadSpecAsync"/>.</summary>
    public ResolvedDashboard Resolved =>
        _resolved ?? throw new InvalidOperationException("Dashboard spec is not loaded.");

    /// <summary>Current filter state bound to the loaded document.</summary>
    public FilterState Filters =>
        _filters ?? throw new InvalidOperationException("Dashboard spec is not loaded.");

    public async Task LoadSpecAsync(string specPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(specPath);

        var fullPath = Path.GetFullPath(specPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("DashSpec file not found.", fullPath);
        }

        var specDirectory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException($"Cannot resolve directory for spec path '{fullPath}'.");

        var text = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
        var document = DashSpecParser.Parse(text, specDirectory);
        var library = SpecLibraryComposer.Load(
            fullPath,
            document.DiagramLibraryPath,
            document.PalettePath,
            specDirectory,
            document);
        var resolved = SpecResolver.Resolve(document, library);

        _document = document;
        _resolved = resolved;
        _filters = DashboardBootstrap.CreateInitialFilters(document, _previewDate);
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_document is null)
        {
            throw new InvalidOperationException("Dashboard spec is not loaded.");
        }

        _filters = DashboardBootstrap.CreateInitialFilters(_document, _previewDate);
        return Task.CompletedTask;
    }

    public Task<string> GetPayloadFingerprintAsync(CancellationToken cancellationToken = default)
    {
        var document = Document;
        var tabIds = document.Tabs.Count > 0
            ? document.Tabs.Select(tab => tab.Id).OrderBy(static id => id, StringComparer.Ordinal).ToArray()
            : new[] { document.Id };

        var canonical = string.Join(
            ';',
            $"dashboard={document.Id}",
            $"cards={document.Cards.Count}",
            $"tabs={string.Join(',', tabIds)}");

        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();

        return Task.FromResult(fingerprint);
    }
}
