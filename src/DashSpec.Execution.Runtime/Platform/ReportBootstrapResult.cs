using DashSpec.Abstractions.Connectors;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime;

namespace DashSpec.Execution.Runtime.Platform;

/// <summary>Resolved spec + runtime connector after bootstrap (ADR-0099).</summary>
public sealed record ReportBootstrapResult(
    DashboardDocument Document,
    SpecLibrary? Library,
    IDataSourceConnector Connector,
    IReadOnlyDictionary<string, FilterDefinition> FilterIndex,
    FilterState Filters,
    IReadOnlyDictionary<string, IReadOnlyList<string>> FieldOptions,
    string SourceLabel,
    string? SpecDirectory,
    string RuntimeConfigPath);


