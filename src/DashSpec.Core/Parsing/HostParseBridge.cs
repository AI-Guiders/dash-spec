using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class HostParseBridge
{
    internal static Func<string, string?, HostDocument>? Parse { get; set; }
}
