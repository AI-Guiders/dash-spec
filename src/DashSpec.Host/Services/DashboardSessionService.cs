using DashSpec.Abstractions.Connectors;
using DashSpec.Abstractions.Query;
using DashSpec.Core.Compilation;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Resolution;
using DashSpec.Core.Runtime;
using DashSpec.Host.Configuration;
using Microsoft.Extensions.Options;

namespace DashSpec.Host.Services;

public sealed class DashboardSessionService(
    ConnectorRegistry connectorRegistry,
    ConnectorPluginManifest pluginManifest,
    DashSpecHostContext hostContext,
    IWebHostEnvironment environment,
    IOptions<DashboardHostOptions> hostOptions,
    ILogger<DashboardSessionService> logger)
{
    private DashboardDocument? _document;
    private IDataSourceConnector? _connector;
    private IReadOnlyDictionary<string, FilterDefinition>? _filterIndex;
    private FilterState? _filters;
    private SpecLibrary? _specLibrary;
    private readonly Dictionary<string, IReadOnlyList<string>> _fieldOptions = new(StringComparer.OrdinalIgnoreCase);

    public SpecLibrary? SpecLibrary => _specLibrary;

    public DashboardDocument Document => _document ?? throw new InvalidOperationException("Dashboard not loaded.");
    public FilterState Filters => _filters ?? throw new InvalidOperationException("Dashboard not loaded.");
    public string ActiveConnectorId => _connector?.Id ?? pluginManifest.DefaultConnectorId;
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

    private async Task LoadFromTextAsync(
        string text,
        string specFullPath,
        string sourceLabel,
        CancellationToken cancellationToken)
    {
        var configPath = DashSpecBootstrap.ResolveRuntimeConfigPath(
            specFullPath,
            text,
            hostContext.DefaultSpecDirectory);
        if (!string.Equals(configPath, hostContext.StartupRuntimeConfigPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Этот дашборд ссылается на другой @config ({Path.GetFileName(configPath)}). " +
                "Смена runtime-конфига в UI пока не поддерживается — укажи spec_path в dash-spec.toml и перезапусти Host.");
        }

        _document = DashSpecParser.Parse(text, Path.GetDirectoryName(specFullPath));
        _specLibrary = LoadSpecLibrary(specFullPath, _document.DiagramLibraryPath);
        _ = SpecResolver.Resolve(_document, _specLibrary);
        _connector = connectorRegistry.Resolve(_document.ConnectorId, pluginManifest.DefaultConnectorId);
        _filterIndex = DashboardBootstrap.IndexFilters(_document);
        _filters = DashboardBootstrap.CreateInitialFilters(_document, DateOnly.FromDateTime(DateTime.UtcNow));
        _fieldOptions.Clear();
        LoadedSpecSource = sourceLabel;

        foreach (var filter in _document.Filters.Where(x => x.Kind is FilterKind.Field))
        {
            try
            {
                var sql = QueryCompiler.BuildDistinctFieldSql(filter);
                _fieldOptions[filter.Name] = await _connector
                    .QueryDistinctStringsAsync(sql, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load field options for filter {FilterName}", filter.Name);
                _fieldOptions[filter.Name] = [];
            }
        }
    }

    public IReadOnlyList<string> GetFieldOptions(string filterName) =>
        _fieldOptions.TryGetValue(filterName, out var values) ? values : [];

    public void ApplyDateFilter(string name, DateOnly from, DateOnly to) =>
        Filters.SetDate(name, from, to);

    public void ApplyFieldFilter(string name, IEnumerable<string> values) =>
        Filters.SetField(name, values.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList());

    public void ApplyTopFilter(string name, int limit) =>
        Filters.SetTop(name, limit);

    public async Task<CardRenderResult> RenderCardAsync(CardDefinition card, CancellationToken cancellationToken = default)
    {
        var connector = _connector ?? throw new InvalidOperationException("Dashboard not loaded.");
        var resolved = CardResolver.Resolve(card, _diagramLibrary, Document.DashboardFilters);
        var effective = resolved.Card;
        var query = QueryCompiler.Compile(effective, Filters, FilterIndex, Document.SqlDialect);
        var rows = await connector.QueryAsync(query, cancellationToken).ConfigureAwait(false);
        var kind = DiagramKindRegistry.Resolve(effective.Diagram.Kind);
        var chartPresentation = kind.DataFamily is DiagramDataFamily.Chart
            ? CardChromeResolver.ResolveChartPresentation(effective, _diagramLibrary)
            : null;
        var seriesTransform = kind.DataFamily is DiagramDataFamily.Chart
            ? CardChromeResolver.ResolveSeriesTransform(effective, _diagramLibrary)
            : null;
        var matrixPresentation = kind.DataFamily is DiagramDataFamily.Matrix
            ? MatrixPresentation.FromCard(effective, _diagramLibrary)
            : null;

        return kind.DataFamily switch
        {
            DiagramDataFamily.Chart =>
                new CardRenderResult(
                    card.Id,
                    card.Title,
                    effective.Diagram.Kind,
                    kind.DataFamily,
                    Chart: ChartDataBuilder.BuildLineOrBar(rows, effective.Diagram, seriesTransform),
                    Placement: card.Placement,
                    ChartPresentation: chartPresentation,
                    BoundFilters: card.BoundFilters,
                    LocalFilters: card.LocalFilters),
            DiagramDataFamily.Table =>
                new CardRenderResult(
                    card.Id,
                    card.Title,
                    effective.Diagram.Kind,
                    kind.DataFamily,
                    Table: ChartDataBuilder.BuildTable(rows, effective.Diagram),
                    Placement: card.Placement,
                    BoundFilters: card.BoundFilters,
                    LocalFilters: card.LocalFilters),
            DiagramDataFamily.Scalar =>
                new CardRenderResult(
                    card.Id,
                    card.Title,
                    effective.Diagram.Kind,
                    kind.DataFamily,
                    Number: FormatNumber(rows, effective.Diagram),
                    Placement: card.Placement,
                    BoundFilters: card.BoundFilters,
                    LocalFilters: card.LocalFilters),
            DiagramDataFamily.Matrix =>
                new CardRenderResult(
                    card.Id,
                    card.Title,
                    effective.Diagram.Kind,
                    kind.DataFamily,
                    Matrix: ChartDataBuilder.BuildHeatmap(rows, effective.Diagram),
                    Placement: card.Placement,
                    MatrixPresentation: matrixPresentation,
                    BoundFilters: card.BoundFilters,
                    LocalFilters: card.LocalFilters),
            _ => throw new ArgumentOutOfRangeException(nameof(card)),
        };
    }

    private static string? FormatNumber(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        DiagramDefinition diagram)
    {
        if (rows.Count == 0)
        {
            return null;
        }

        return Convert.ToString(rows[0].GetValueOrDefault(DiagramBindings.Column(diagram, "value")));
    }

    private SpecLibrary? LoadSpecLibrary(string specFullPath, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var path = DashSpecBootstrap.ResolveSpecLibraryPath(
            specFullPath,
            relativePath,
            hostContext.DefaultSpecDirectory);
        return SpecLibrary.LoadFile(path);
    }
}

public sealed record CardRenderResult(
    string Id,
    string Title,
    string DiagramKind,
    DiagramDataFamily DataFamily,
    ChartPayload? Chart = null,
    TablePayload? Table = null,
    string? Number = null,
    string? Error = null,
    bool Loading = false,
    IReadOnlyList<string>? BoundFilters = null,
    IReadOnlyList<string>? LocalFilters = null,
    PlacementDefinition? Placement = null,
    ChartPresentation? ChartPresentation = null,
    MatrixPayload? Matrix = null,
    MatrixPresentation? MatrixPresentation = null);

public sealed class DashboardHostOptions
{
    public const string SectionName = "Dashboard";

    public string SpecPath { get; set; } = string.Empty;
}
