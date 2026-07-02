using DashSpec.Abstractions.Connectors;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using DashSpec.Host.Configuration;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Models;
using Microsoft.Extensions.Options;

namespace DashSpec.Host.Services;

public sealed class DashboardSessionService(
    IDashboardSpecLoader specLoader,
    ICardRenderer cardRenderService,
    DashSpecHostContext hostContext,
    IWebHostEnvironment environment,
    IOptions<DashboardHostOptions> hostOptions) : IDashboardSession
{
    private DashboardDocument? _document;
    private IDataSourceConnector? _connector;
    private IReadOnlyDictionary<string, FilterDefinition>? _filterIndex;
    private FilterState? _filters;
    private SpecLibrary? _specLibrary;
    private string? _specDirectory;
    private Dictionary<string, IReadOnlyList<string>> _fieldOptions = new(StringComparer.OrdinalIgnoreCase);

    public SpecLibrary? SpecLibrary => _specLibrary;

    public DashboardDocument Document => _document ?? throw new InvalidOperationException("Dashboard not loaded.");
    public FilterState Filters => _filters ?? throw new InvalidOperationException("Dashboard not loaded.");
    public string ActiveConnectorId => _connector?.Id ?? throw new InvalidOperationException("Dashboard not loaded.");
    public string? LoadedSpecSource { get; private set; }

    public IReadOnlyDictionary<string, FilterDefinition> FilterIndex =>
        _filterIndex ?? throw new InvalidOperationException("Dashboard not loaded.");

    public async Task LoadAsync(string? specRelativePath = null, CancellationToken cancellationToken = default)
    {
        var relative = specRelativePath
            ?? hostContext.DefaultSpecRelativePath
            ?? hostOptions.Value.SpecPath;
        if (string.IsNullOrWhiteSpace(relative))
        {
            throw new InvalidOperationException("Dashboard spec path is not configured.");
        }

        relative = relative.Replace('\\', '/');
        var path = DashSpecBootstrap.ResolveSpecPath(environment.ContentRootPath, relative);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("DashSpec file not found.", path);
        }

        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        await LoadFromTextAsync(text, path, Path.GetFileName(path), cancellationToken).ConfigureAwait(false);
    }

    public async Task LoadFromUploadAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default)
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
        await LoadFromTextAsync(text, savedPath, safeName, cancellationToken).ConfigureAwait(false);
    }

    public IReadOnlyList<string> GetFieldOptions(string filterName) =>
        _fieldOptions.TryGetValue(filterName, out var values) ? values : [];

    public void ApplyDateFilter(string name, DateOnly from, DateOnly to) =>
        Filters.SetDate(name, from, to);

    public void ApplyFieldFilter(string name, IEnumerable<string> values) =>
        Filters.SetField(name, values.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList());

    public void ApplyTopFilter(string name, int limit) =>
        Filters.SetTop(name, limit);

    public Task<CardRenderResult> RenderCardAsync(CardDefinition card, CancellationToken cancellationToken = default) =>
        cardRenderService.RenderAsync(
            card,
            Document,
            Filters,
            FilterIndex,
            _specLibrary,
            _connector ?? throw new InvalidOperationException("Dashboard not loaded."),
            _specDirectory,
            cancellationToken);

    private async Task LoadFromTextAsync(
        string text,
        string specFullPath,
        string sourceLabel,
        CancellationToken cancellationToken)
    {
        var loaded = await specLoader.LoadFromTextAsync(text, specFullPath, sourceLabel, cancellationToken)
            .ConfigureAwait(false);

        _document = loaded.Document;
        _specLibrary = loaded.Library;
        _connector = loaded.Connector;
        _filterIndex = loaded.FilterIndex;
        _filters = loaded.Filters;
        _fieldOptions = loaded.FieldOptions.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        _specDirectory = loaded.SpecDirectory;
        LoadedSpecSource = loaded.SourceLabel;
    }
}
