using DashSpec.Abstractions.Connectors;
using DashSpec.Abstractions.Hosting;
using DashSpec.Abstractions.Plugins;
using DashSpec.Core.Catalog;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Platform;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Connectors;
using DashSpec.Host.Services.Loading;
using DashSpec.Host.Services.Presentation;
using DashSpec.Viz;
using DashSpec.Viz.Platform;
using HostAbstractions = DashSpec.Host.Services.Abstractions;

namespace DashSpec.Host.Services;

public sealed class DashboardSessionService(
    IDashboardSpecLoader specLoader,
    RuntimeConnectorResolver runtimeConnectorResolver,
    HostAbstractions.ICardRenderer cardRenderService,
    ICardViewState cardViewState,
    CatalogSourceState catalogState,
    IWebHostEnvironment environment,
    IHostPathResolver pathResolver,
    CardLocalFilterUiStore cardLocalFilters) : HostAbstractions.IDashboardSession
{
    private DashboardDocument? _document;
    private IDataSourceConnector? _connector;
    private IReadOnlyDictionary<string, FilterDefinition>? _filterIndex;
    private FilterState? _filters;
    private SpecLibrary? _specLibrary;
    private string? _specDirectory;
    private string? _runtimeConfigPath;
    private string? _activeCatalogEntryId;
    private string? _currentSpecReference;
    private Dictionary<string, IReadOnlyList<string>> _fieldOptions = new(StringComparer.OrdinalIgnoreCase);

    public SpecLibrary? SpecLibrary => _specLibrary;

    public DashboardDocument Document => _document ?? throw new InvalidOperationException("Dashboard not loaded.");
    public FilterState Filters => _filters ?? throw new InvalidOperationException("Dashboard not loaded.");
    public string ActiveConnectorId => _connector?.Id ?? throw new InvalidOperationException("Dashboard not loaded.");
    public string? LoadedSpecSource { get; private set; }
    public string? ActiveCatalogEntryId => _activeCatalogEntryId;
    public string? CurrentSpecReference => _currentSpecReference;

    public IReadOnlyDictionary<string, FilterDefinition> FilterIndex =>
        _filterIndex ?? throw new InvalidOperationException("Dashboard not loaded.");

    public async Task LoadAsync(
        string? specRelativePath = null,
        CancellationToken cancellationToken = default,
        ReportLoadOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(specRelativePath) &&
            string.IsNullOrWhiteSpace(_activeCatalogEntryId))
        {
            await LoadCatalogEntryAsync(catalogState.Current.Document.DefaultEntryId, cancellationToken, options)
                .ConfigureAwait(false);
            return;
        }

        var relative = specRelativePath ?? _currentSpecReference;
        if (string.IsNullOrWhiteSpace(relative))
        {
            throw new InvalidOperationException("Dashboard spec reference is not configured.");
        }

        relative = relative.Replace('\\', '/');
        var path = pathResolver.ResolveSpecPath(environment.ContentRootPath, relative);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("DashSpec file not found.", path);
        }

        _currentSpecReference = relative;
        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        await LoadFromTextAsync(text, path, Path.GetFileName(path), cancellationToken, options).ConfigureAwait(false);
    }

    public async Task LoadCatalogEntryAsync(
        string entryId,
        CancellationToken cancellationToken = default,
        ReportLoadOptions? options = null)
    {
        var specFullPath = catalogState.Current.ResolveEntrySpecFullPath(entryId);
        _activeCatalogEntryId = entryId;
        _currentSpecReference = pathResolver.ToHostSpecReference(environment.ContentRootPath, specFullPath);
        var text = await File.ReadAllTextAsync(specFullPath, cancellationToken).ConfigureAwait(false);
        await LoadFromTextAsync(text, specFullPath, Path.GetFileName(specFullPath), cancellationToken, options)
            .ConfigureAwait(false);
    }

    public async Task LoadFromUploadAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default,
        ReportLoadOptions? options = null)
    {
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        var uploadsDir = Path.Combine(environment.ContentRootPath, "uploads");
        Directory.CreateDirectory(uploadsDir);

        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "upload.dashspec";
        }

        if (!safeName.EndsWith(".dashspec", StringComparison.OrdinalIgnoreCase))
        {
            safeName += ".dashspec";
        }

        var savedPath = Path.Combine(uploadsDir, safeName);
        await File.WriteAllTextAsync(savedPath, text, cancellationToken).ConfigureAwait(false);
        await LoadFromTextAsync(text, savedPath, safeName, cancellationToken, options).ConfigureAwait(false);
    }


    Task IReportSession.LoadCatalogEntryAsync(string entryId, CancellationToken cancellationToken, DashSpec.Core.Platform.ReportLoadOptions? options) =>
        LoadCatalogEntryAsync(entryId, cancellationToken, options as SpecLoadOptions);

    Task IReportSession.LoadFromUploadAsync(Stream stream, string fileName, CancellationToken cancellationToken, DashSpec.Core.Platform.ReportLoadOptions? options) =>
        LoadFromUploadAsync(stream, fileName, cancellationToken, options as SpecLoadOptions);
    public async Task RefreshFieldOptionsAsync(CancellationToken cancellationToken = default)
    {
        if (_document is null || _connector is null)
        {
            return;
        }

        var loaded = await specLoader
            .LoadFieldOptionsAsync(_document, _connector, cancellationToken)
            .ConfigureAwait(false);
        _fieldOptions = loaded.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> GetFieldOptions(string filterName) =>
        _fieldOptions.TryGetValue(filterName, out var values) ? values : [];

    public void ApplyDateFilter(string name, DateOnly from, DateOnly to) =>
        Filters.SetDate(name, from, to);

    public void ApplyFieldFilter(string name, IEnumerable<string> values) =>
        Filters.SetField(name, values.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList());

    public void ApplyTopFilter(string name, int limit) =>
        Filters.SetTop(name, limit);

    public Task<CardRenderResult> RenderCardAsync(CardDefinition card, CancellationToken cancellationToken = default)
    {
        var effectiveCard = DocumentFlowBinder.MaterializeCard(ResolveEffectiveCard(card), Document);
        var queryFilters = cardLocalFilters.ComposeQueryFilters(Filters, card, FilterIndex);
        var providerId = FlowCardExecution.ResolveProviderId(effectiveCard);
        var connector = runtimeConnectorResolver.Resolve(
            _runtimeConfigPath ?? throw new InvalidOperationException("Dashboard not loaded."),
            providerId);
        return cardRenderService.RenderAsync(
            effectiveCard,
            Document,
            queryFilters,
            FilterIndex,
            _specLibrary,
            connector,
            _specDirectory,
            cancellationToken);
    }

    public Task<IReadOnlyDictionary<string, CardSlotRenderResult>> RenderInteriorSlotsAsync(
        CardDefinition card,
        CancellationToken cancellationToken = default)
    {
        var effectiveCard = DocumentFlowBinder.MaterializeCard(ResolveEffectiveCard(card), Document);
        var queryFilters = cardLocalFilters.ComposeQueryFilters(Filters, card, FilterIndex);
        var providerId = FlowCardExecution.ResolveProviderId(effectiveCard);
        var connector = runtimeConnectorResolver.Resolve(
            _runtimeConfigPath ?? throw new InvalidOperationException("Dashboard not loaded."),
            providerId);
        return cardRenderService.RenderInteriorSlotsAsync(
            effectiveCard,
            Document,
            queryFilters,
            FilterIndex,
            _specLibrary,
            connector,
            _specDirectory,
            cancellationToken);
    }

    private CardDefinition ResolveEffectiveCard(CardDefinition card)
    {
        var activeView = cardViewState.GetActiveView(card.Id)
            ?? CardViewSwitchApplier.ResolveDefaultViewId(card.ExtensionBlocks);
        return CardViewSwitchApplier.Apply(card, activeView);
    }

    private async Task LoadFromTextAsync(
        string text,
        string specFullPath,
        string sourceLabel,
        CancellationToken cancellationToken,
        ReportLoadOptions? options = null)
    {
        var loaded = await specLoader
            .LoadFromTextAsync(text, specFullPath, sourceLabel, cancellationToken, options)
            .ConfigureAwait(false);

        _document = loaded.Document;
        _specLibrary = loaded.Library;
        _connector = loaded.Connector;
        _filterIndex = loaded.FilterIndex;
        _filters = loaded.Filters;
        _fieldOptions = loaded.FieldOptions.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        _specDirectory = loaded.SpecDirectory;
        _runtimeConfigPath = loaded.RuntimeConfigPath;
        LoadedSpecSource = loaded.SourceLabel;
    }
}
