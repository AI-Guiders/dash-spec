using DashSpec.Abstractions.Connectors;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime;

namespace DashSpec.Host.Services.Models;

/// <summary>Host bootstrap payload (same shape as <see cref="Execution.Runtime.Platform.ReportBootstrapResult"/>).</summary>
public sealed record LoadedDashboard(
    DashboardDocument Document,
    SpecLibrary? Library,
    IDataSourceConnector Connector,
    IReadOnlyDictionary<string, FilterDefinition> FilterIndex,
    FilterState Filters,
    IReadOnlyDictionary<string, IReadOnlyList<string>> FieldOptions,
    string SourceLabel,
    string? SpecDirectory,
    string RuntimeConfigPath);
