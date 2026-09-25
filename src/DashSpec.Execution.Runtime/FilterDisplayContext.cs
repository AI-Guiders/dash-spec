using DashSpec.Core.Model;

namespace DashSpec.Execution.Runtime;

/// <summary>Runtime filter UI state for display title resolution (ADR-0058).</summary>
public sealed record FilterDisplayContext(
    IReadOnlyDictionary<string, FilterDefinition> FilterIndex,
    IReadOnlyDictionary<string, DateOnly> DateFrom,
    IReadOnlyDictionary<string, DateOnly> DateTo,
    IReadOnlyDictionary<string, HashSet<string>> SelectedFields,
    IReadOnlyDictionary<string, int> TopLimits);
