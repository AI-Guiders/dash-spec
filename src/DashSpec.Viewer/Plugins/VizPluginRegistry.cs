using DashSpec.Abstractions.Viz;

namespace DashSpec.Viewer.Plugins;

/// <summary>
/// Renderer resolution per render stack (ADR-0112): the author never picks a renderer —
/// the effective stack routes each data family and may delegate a single kind to a
/// guest renderer. Unknown stack or family falls back to the builtin family defaults.
/// </summary>
public sealed class VizPluginRegistry
{
    private static readonly IReadOnlyDictionary<DiagramDataFamily, string> BuiltInDefaults =
        new Dictionary<DiagramDataFamily, string>
        {
            [DiagramDataFamily.Chart] = VizPluginIds.ChartJs,
            [DiagramDataFamily.Table] = VizPluginIds.TableHtml,
            [DiagramDataFamily.Scalar] = VizPluginIds.ScalarHtml,
            [DiagramDataFamily.Matrix] = VizPluginIds.MatrixCanvas,
            [DiagramDataFamily.Gantt] = VizPluginIds.GanttHtml,
        };

    private readonly IReadOnlyDictionary<string, IVizPlugin> _byId;
    private readonly IReadOnlyList<IRenderStack> _stacks;
    private readonly IRenderStackSettings _settings;

    public VizPluginRegistry(
        IEnumerable<IVizPlugin> plugins,
        IEnumerable<IRenderStack> stacks,
        IRenderStackSettings settings)
    {
        _byId = plugins.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        _stacks = stacks.ToList();
        _settings = settings;
    }

    public string Resolve(DiagramDataFamily family, string? kindId)
    {
        var stackName = _settings.HostStack?.Trim();
        var stack = _stacks.FirstOrDefault(s =>
            string.Equals(s.Name, stackName, StringComparison.OrdinalIgnoreCase));

        if (stack is not null)
        {
            // Kind delegation first: the stack says "this kind is not mine — a guest renders it".
            if (!string.IsNullOrWhiteSpace(kindId) &&
                stack.KindPlugins.TryGetValue(kindId.Trim(), out var kindPick) &&
                _byId.ContainsKey(kindPick))
            {
                return kindPick;
            }

            if (stack.FamilyPlugins.TryGetValue(family.ToString(), out var pick) &&
                _byId.ContainsKey(pick))
            {
                return pick;
            }
        }

        return BuiltInDefaults.TryGetValue(family, out var fallback)
            ? fallback
            : throw new InvalidOperationException($"No viz plugin registered for data family {family}.");
    }
}
