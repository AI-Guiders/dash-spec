using DashSpec.Abstractions.Viz;
using DashSpec.Core.Model;

namespace DashSpec.Host.Plugins;

public sealed class VizPluginRegistry
{
    private readonly IReadOnlyDictionary<string, IVizPlugin> _byId;
    private static readonly IReadOnlyDictionary<DiagramDataFamily, string> DefaultPluginIds =
        new Dictionary<DiagramDataFamily, string>
        {
            [DiagramDataFamily.Chart] = VizPluginIds.ChartJs,
            [DiagramDataFamily.Table] = VizPluginIds.TableHtml,
            [DiagramDataFamily.Scalar] = VizPluginIds.ScalarHtml,
            [DiagramDataFamily.Matrix] = VizPluginIds.MatrixCanvas,
        };

    public VizPluginRegistry(IEnumerable<IVizPlugin> plugins)
    {
        _byId = plugins.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
    }

    public string Resolve(string? requestedPluginId, DiagramDataFamily family)
    {
        if (!string.IsNullOrWhiteSpace(requestedPluginId) &&
            _byId.ContainsKey(requestedPluginId))
        {
            return requestedPluginId;
        }

        return DefaultPluginIds.TryGetValue(family, out var fallback)
            ? fallback
            : throw new InvalidOperationException($"No viz plugin registered for data family {family}.");
    }
}
