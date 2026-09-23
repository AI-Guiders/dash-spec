namespace DashSpec.Core.Model;

public enum DiagramDataFamily
{
    Chart,
    Table,
    Scalar,
    Matrix,
    Gantt,
}

public sealed record DiagramKindInfo(
    string Id,
    DiagramDataFamily DataFamily,
    bool SupportsTopLimit = false,
    bool AllowExtensionProperties = false);
