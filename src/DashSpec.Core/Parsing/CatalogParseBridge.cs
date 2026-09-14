using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class CatalogParseBridge
{
    internal static Func<string, CatalogDocument>? Parse { get; set; }
}
