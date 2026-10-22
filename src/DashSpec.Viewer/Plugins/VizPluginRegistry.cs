using DashSpec.Abstractions.Viz;
using Microsoft.Extensions.Configuration;

namespace DashSpec.Viewer.Plugins;

/// <summary>
/// Renderer resolution per data family ([ADR-0111] visual pilot, operator 2026-10-22):
/// the renderer is a host decision, not a spec one — the family defaults come from the
/// host configuration (<c>[viz]</c> in dash-spec.toml → Viz:Chart/Table/Matrix/Gantt/Scalar
/// keys), falling back to the built-in family defaults when the host says nothing.
/// A diagram's own render (a preset's Render) still wins as an explicit override.
/// </summary>
public sealed class VizPluginRegistry
{
    private readonly IReadOnlyDictionary<string, IVizPlugin> _byId;

    private static readonly IReadOnlyDictionary<DiagramDataFamily, string> BuiltInDefaults =
        new Dictionary<DiagramDataFamily, string>
        {
            [DiagramDataFamily.Chart] = VizPluginIds.ChartJs,
            [DiagramDataFamily.Table] = VizPluginIds.TableHtml,
            [DiagramDataFamily.Scalar] = VizPluginIds.ScalarHtml,
            [DiagramDataFamily.Matrix] = VizPluginIds.MatrixCanvas,
            [DiagramDataFamily.Gantt] = VizPluginIds.GanttHtml,
        };

    private readonly IReadOnlyDictionary<DiagramDataFamily, string> _hostDefaults;

    public VizPluginRegistry(IEnumerable<IVizPlugin> plugins, IConfiguration configuration)
    {
        _byId = plugins.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        _hostDefaults = new Dictionary<DiagramDataFamily, string>
        {
            [DiagramDataFamily.Chart] = HostPick(configuration, "Viz:Chart", DiagramDataFamily.Chart),
            [DiagramDataFamily.Table] = HostPick(configuration, "Viz:Table", DiagramDataFamily.Table),
            [DiagramDataFamily.Scalar] = HostPick(configuration, "Viz:Scalar", DiagramDataFamily.Scalar),
            [DiagramDataFamily.Matrix] = HostPick(configuration, "Viz:Matrix", DiagramDataFamily.Matrix),
            [DiagramDataFamily.Gantt] = HostPick(configuration, "Viz:Gantt", DiagramDataFamily.Gantt),
        };
    }

    private string HostPick(IConfiguration configuration, string key, DiagramDataFamily family)
    {
        var hostPick = configuration[key];
        return !string.IsNullOrWhiteSpace(hostPick) && _byId.ContainsKey(hostPick)
            ? hostPick
            : BuiltInDefaults[family];
    }

    public string Resolve(string? requestedPluginId, DiagramDataFamily family)
    {
        if (!string.IsNullOrWhiteSpace(requestedPluginId) &&
            _byId.ContainsKey(requestedPluginId))
        {
            return requestedPluginId;
        }

        return _hostDefaults.TryGetValue(family, out var fallback)
            ? fallback
            : throw new InvalidOperationException($"No viz plugin registered for data family {family}.");
    }
}
