#nullable enable
using AIGuiders.Platform.CommandPlane.Commands;
using DashSpec.Core.Model;
using DashSpec.Host.Services.Presentation;

namespace DashSpec.Host.Commands;

/// <summary>Invocation payload for dashboard filter commands (DASHSPEC-ADR-0043).</summary>
public sealed class DashboardFilterContext : ICommandContext
{
    public required string ReportId { get; init; }

    public required IReadOnlyDictionary<string, FilterDefinition> FilterIndex { get; init; }

    public required IReadOnlyList<string> ToolbarFilterNames { get; init; }

    public required IReadOnlyDictionary<string, string> CommandAliases { get; init; }

    public required DashboardFilterUiState UiState { get; init; }

    public required Func<string, IReadOnlyList<string>> GetFieldOptions { get; init; }

    public DateOnly TodayUtc { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public string CanonicalPath { get; set; } = "";

    public string ArgTail { get; set; } = "";

    public void ApplyDate(string filterName, DateOnly from, DateOnly to)
    {
        UiState.DateFrom[filterName] = from;
        UiState.DateTo[filterName] = to;
    }

    public void ApplyField(string filterName, IEnumerable<string> values) =>
        UiState.SelectedFields[filterName] = values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
