using DashSpec.Abstractions.Viz;
using DashSpec.Core.Model;

namespace DashSpec.Host.Plugins;

public sealed class VizPluginRegistry
{
    private readonly IReadOnlyDictionary<string, IVizPlugin> _byId;
    private readonly IReadOnlyDictionary<DiagramDataFamily, string> _defaults;

    public VizPluginRegistry(IEnumerable<IVizPlugin> plugins)
    {
        var list = plugins.ToList();
        _byId = list.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        _defaults = list.ToDictionary(
            x => ParseFamily(x.DataFamily),
            x => x.Id);
    }

    public string Resolve(string? requestedPluginId, DiagramDataFamily family)
    {
        if (!string.IsNullOrWhiteSpace(requestedPluginId) &&
            _byId.ContainsKey(requestedPluginId))
        {
            return requestedPluginId;
        }

        return _defaults.TryGetValue(family, out var fallback)
            ? fallback
            : throw new InvalidOperationException($"No viz plugin registered for data family {family}.");
    }

    private static DiagramDataFamily ParseFamily(string value) =>
        value.ToLowerInvariant() switch
        {
            "chart" => DiagramDataFamily.Chart,
            "table" => DiagramDataFamily.Table,
            "scalar" => DiagramDataFamily.Scalar,
            "matrix" => DiagramDataFamily.Matrix,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown viz data family."),
        };
}
