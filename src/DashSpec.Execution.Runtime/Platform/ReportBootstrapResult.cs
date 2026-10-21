using DashSpec.Abstractions.Connectors;
using DashSpec.Execution.Runtime;
using DashSpec.Modeling.Parse.Layout;

namespace DashSpec.Execution.Runtime.Platform;

/// <summary>Resolved spec + runtime connector after bootstrap (ADR-0099); the assembled slot plane rides it ([ADR-0111]).</summary>
public sealed record ReportBootstrapResult(
    DashboardDocument Document,
    SpecLibrary? Library,
    IDataSourceConnector Connector,
    IReadOnlyDictionary<string, FilterDefinition> FilterIndex,
    FilterState Filters,
    IReadOnlyDictionary<string, IReadOnlyList<string>> FieldOptions,
    string SourceLabel,
    string? SpecDirectory,
    string RuntimeConfigPath,
    SlotPlane? SlotPlane = null);


