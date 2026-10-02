using System;

namespace DashSpec.Analyzers;

/// <summary>Layer detection via LogicalPath-compatible normalization (ADR-0085 / GUIDERS-ADR-0050).</summary>
internal static class DashSpecLayerPaths
{
    private const string ConnectorsPrefix = "connectors";
    private const string ExecutionRuntimePrefix = "src/DashSpec.Execution.Runtime";
    private const string ExecutionCompilationPrefix = "src/DashSpec.Execution.Compilation";
    private const string CorePrefix = "src/DashSpec.Core";
    private const string AbstractionsPrefix = "src/DashSpec.Abstractions";
    private const string HostPrefix = "src/DashSpec.Host";
    private const string AcquisitionDataPrefix = "src/DashSpec.Abstractions/Data/Acquisition";

    public static bool IsConnectorAcquisitionLayer(string? physicalPath) =>
        LogicalPathCompat.ContainsLayerSegment(physicalPath, ConnectorsPrefix)
        || (physicalPath?.Contains("DashSpec.Connector.", StringComparison.Ordinal) ?? false);

    public static bool IsExecutionRuntimeLayer(string? physicalPath) =>
        LogicalPathCompat.ContainsLayerSegment(physicalPath, ExecutionRuntimePrefix);

    public static bool IsHostProject(string? physicalPath) =>
        LogicalPathCompat.ContainsLayerSegment(physicalPath, HostPrefix);

    public static bool IsUntypedRowBagForbiddenLayer(string? physicalPath) =>
        IsDataPlaneLayer(physicalPath)
        || (LogicalPathCompat.ContainsLayerSegment(physicalPath, AbstractionsPrefix)
            && !IsAcquisitionMaterializationLayer(physicalPath));

    public static bool IsTestProject(string? physicalPath) =>
        physicalPath?.Contains("/tests/", StringComparison.OrdinalIgnoreCase) == true
        || physicalPath?.Contains("\\tests\\", StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsAcquisitionMaterializationLayer(string? physicalPath) =>
        IsConnectorAcquisitionLayer(physicalPath)
        || LogicalPathCompat.ContainsLayerSegment(physicalPath, AcquisitionDataPrefix);

    public static bool IsDataPlaneLayer(string? physicalPath)
    {
        if (IsTestProject(physicalPath) || IsAcquisitionMaterializationLayer(physicalPath))
        {
            return false;
        }

        return LogicalPathCompat.ContainsLayerSegment(physicalPath, ExecutionRuntimePrefix)
               || LogicalPathCompat.ContainsLayerSegment(physicalPath, ExecutionCompilationPrefix)
               || LogicalPathCompat.ContainsLayerSegment(physicalPath, CorePrefix)
               || IsHostProject(physicalPath)
               || LogicalPathCompat.ContainsLayerSegment(physicalPath, AbstractionsPrefix);
    }
}
