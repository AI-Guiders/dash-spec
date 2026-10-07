using DashSpec.Abstractions.Connectors;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Platform;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime;
using DashSpec.Execution.Runtime.Platform;

namespace DashSpec.Viz.Platform;

/// <summary>Headless <see cref="IReportSession"/> over <see cref="IReportSpecBootstrap"/> (ADR-0099 L3).</summary>
public sealed class HeadlessReportSession(
    IReportSpecBootstrap bootstrap,
    IReportCardRenderer? cardRenderer = null) : IReportSession
{
    private DashboardDocument? _document;
    private IDataSourceConnector? _connector;
    private IReadOnlyDictionary<string, FilterDefinition>? _filterIndex;
    private FilterState? _filters;
    private SpecLibrary? _specLibrary;
    private string? _specDirectory;
    private string? _runtimeConfigPath;
    private Dictionary<string, IReadOnlyList<string>> _fieldOptions = new(StringComparer.OrdinalIgnoreCase);

    public SpecLibrary? SpecLibrary => _specLibrary;
    public DashboardDocument Document => _document ?? throw new InvalidOperationException("Report not loaded.");
    public FilterState Filters => _filters ?? throw new InvalidOperationException("Report not loaded.");
    public string ActiveConnectorId => _connector?.Id ?? throw new InvalidOperationException("Report not loaded.");
    public string? LoadedSpecSource { get; private set; }
    public string? ActiveCatalogEntryId => null;
    public string? CurrentSpecReference { get; private set; }

    public IReadOnlyDictionary<string, FilterDefinition> FilterIndex =>
        _filterIndex ?? throw new InvalidOperationException("Report not loaded.");

    public async Task LoadFromTextAsync(
        string text,
        string specFullPath,
        string sourceLabel = "headless",
        CancellationToken cancellationToken = default,
        ReportLoadOptions? options = null)
    {
        var loaded = await bootstrap
            .LoadFromTextAsync(text, specFullPath, sourceLabel, cancellationToken, options)
            .ConfigureAwait(false);
        ApplyBootstrapResult(loaded, specFullPath);
    }

    public Task LoadAsync(
        string? specRelativePath = null,
        CancellationToken cancellationToken = default,
        ReportLoadOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(specRelativePath))
        {
            throw new NotSupportedException("Headless session requires an absolute or relative file path.");
        }

        var path = Path.GetFullPath(specRelativePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("DashSpec file not found.", path);
        }

        CurrentSpecReference = path;
        return LoadFromFileAsync(path, cancellationToken, options);
    }

    public Task LoadCatalogEntryAsync(
        string entryId,
        CancellationToken cancellationToken = default,
        ReportLoadOptions? options = null) =>
        throw new NotSupportedException("Catalog load requires a surface host.");

    public Task LoadFromUploadAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default,
        ReportLoadOptions? options = null) =>
        throw new NotSupportedException("Upload load requires a surface host.");

    public async Task RefreshFieldOptionsAsync(CancellationToken cancellationToken = default)
    {
        if (_document is null || _connector is null || _runtimeConfigPath is null)
        {
            return;
        }

        var loaded = await bootstrap
            .LoadFieldOptionsAsync(_document, _connector, _runtimeConfigPath, cancellationToken)
            .ConfigureAwait(false);
        _fieldOptions = loaded.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> GetFieldOptions(string filterName) =>
        _fieldOptions.TryGetValue(filterName, out var values) ? values : [];

    public void ApplyDateFilter(string name, DateOnly from, DateOnly to) => Filters.SetDate(name, from, to);

    public void ApplyFieldFilter(string name, IEnumerable<string> values) =>
        Filters.SetField(name, values.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList());

    public void ApplyTopFilter(string name, int limit) => Filters.SetTop(name, limit);

    public Task<CardRenderResult> RenderCardAsync(CardDefinition card, CancellationToken cancellationToken = default)
    {
        if (cardRenderer is null)
        {
            throw new InvalidOperationException("No IReportCardRenderer registered for headless render.");
        }

        return cardRenderer.RenderAsync(
            card,
            Document,
            Filters,
            FilterIndex,
            _specLibrary,
            _connector ?? throw new InvalidOperationException("Report not loaded."),
            _specDirectory,
            cancellationToken);
    }

    public Task<IReadOnlyDictionary<string, CardSlotRenderResult>> RenderInteriorSlotsAsync(
        CardDefinition card,
        CancellationToken cancellationToken = default)
    {
        if (cardRenderer is null)
        {
            throw new InvalidOperationException("No IReportCardRenderer registered for headless render.");
        }

        return cardRenderer.RenderInteriorSlotsAsync(
            card,
            Document,
            Filters,
            FilterIndex,
            _specLibrary,
            _connector ?? throw new InvalidOperationException("Report not loaded."),
            _specDirectory,
            cancellationToken);
    }

    private async Task LoadFromFileAsync(string path, CancellationToken cancellationToken, ReportLoadOptions? options)
    {
        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        await LoadFromTextAsync(text, path, Path.GetFileName(path), cancellationToken, options).ConfigureAwait(false);
    }

    private void ApplyBootstrapResult(ReportBootstrapResult loaded, string specFullPath)
    {
        _document = loaded.Document;
        _specLibrary = loaded.Library;
        _connector = loaded.Connector;
        _filterIndex = loaded.FilterIndex;
        _filters = loaded.Filters;
        _fieldOptions = loaded.FieldOptions.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        _specDirectory = loaded.SpecDirectory;
        _runtimeConfigPath = loaded.RuntimeConfigPath;
        LoadedSpecSource = loaded.SourceLabel;
        CurrentSpecReference = specFullPath;
    }
}
