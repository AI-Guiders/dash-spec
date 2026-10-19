
namespace DashSpec.Execution.Parsing;

internal static class HostParseBridge
{
    internal static Func<string, string?, HostDocument>? Parse { get; set; }
}
