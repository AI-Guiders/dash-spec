namespace DashSpec.Core.Model;

public enum DiagramDataFamily
{
    Chart,
    Table,
    Scalar,
    Matrix,
}

public sealed record DiagramKindInfo(
    string Id,
    DiagramDataFamily DataFamily,
    bool SupportsTopLimit = false,
    bool AllowExtensionProperties = false);
