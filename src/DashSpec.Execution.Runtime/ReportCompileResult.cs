
namespace DashSpec.Execution.Runtime;

/// <summary>Document compile output (ADR-0098 front-end via <see cref="IReportCompiler"/>).</summary>
public sealed record ReportCompileResult(
    DashboardDocument Document,
    IReadOnlyList<DashSpecDiagnostic> Diagnostics);
