using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using DashSpec.Host.Services.Loading;
using DashSpec.Host.Services.Models;

namespace DashSpec.Host.Services.Abstractions;

public interface IDashboardSession
{
    SpecLibrary? SpecLibrary { get; }
    DashboardDocument Document { get; }
    FilterState Filters { get; }
    string ActiveConnectorId { get; }
    string? LoadedSpecSource { get; }
    string? ActiveCatalogEntryId { get; }
    string? CurrentSpecReference { get; }
    IReadOnlyDictionary<string, FilterDefinition> FilterIndex { get; }

    Task LoadAsync(
        string? specRelativePath = null,
        CancellationToken cancellationToken = default,
        SpecLoadOptions? options = null);

    Task LoadCatalogEntryAsync(
        string entryId,
        CancellationToken cancellationToken = default,
        SpecLoadOptions? options = null);

    Task LoadFromUploadAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default,
        SpecLoadOptions? options = null);

    Task RefreshFieldOptionsAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<string> GetFieldOptions(string filterName);

    void ApplyDateFilter(string name, DateOnly from, DateOnly to);

    void ApplyFieldFilter(string name, IEnumerable<string> values);

    void ApplyTopFilter(string name, int limit);

    Task<CardRenderResult> RenderCardAsync(CardDefinition card, CancellationToken cancellationToken = default);
}
