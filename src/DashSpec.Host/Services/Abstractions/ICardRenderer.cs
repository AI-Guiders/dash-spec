using DashSpec.Abstractions.Connectors;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime;
using DashSpec.Viz;

namespace DashSpec.Host.Services.Abstractions;

public interface ICardRenderer
{
    Task<CardRenderResult> RenderAsync(
        CardDefinition card,
        DashboardDocument document,
        FilterState filters,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex,
        SpecLibrary? library,
        IDataSourceConnector connector,
        string? specDirectory = null,
        CancellationToken cancellationToken = default);

    /// <summary>Re-query and render non-primary interior slots only (heatmap cell drill).</summary>
    Task<IReadOnlyDictionary<string, CardSlotRenderResult>> RenderInteriorSlotsAsync(
        CardDefinition card,
        DashboardDocument document,
        FilterState filters,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex,
        SpecLibrary? library,
        IDataSourceConnector connector,
        string? specDirectory = null,
        CancellationToken cancellationToken = default);
}
