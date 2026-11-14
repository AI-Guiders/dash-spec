using DashSpec.Abstractions.Viz;

namespace DashSpec.Composition.Plugins;

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
    private readonly Lock _fallbackLock = new();
    private readonly HashSet<string> _announcedFallbacks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>DS0582: fires once per distinct (stack, family/kind, missing plugin) when the
    /// stack's pick is not active (not in the bundle) and rendering silently falls back to
    /// the builtin default. The host subscribes and logs — a missing bundle entry is an
    /// operator decision to revisit, not noise to swallow (the empty-cards class).</summary>
    public event Action<string>? SilentFallback;

    public VizPluginRegistry(
        IEnumerable<IVizPlugin> plugins,
        IEnumerable<IRenderStack> stacks,
        IRenderStackSettings settings)
    {
        _byId = plugins.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        _stacks = stacks.ToList();
        _settings = settings;
    }

    private void AnnounceFallback(string stack, string family, string? kindId, string missing)
    {
        var key = $"{stack}|{kindId ?? family}|{missing}";
        lock (_fallbackLock)
        {
            if (!_announcedFallbacks.Add(key))
            {
                return;
            }
        }

        var fallback = BuiltInDefaults.TryGetValue(
            Enum.TryParse<DiagramDataFamily>(family, ignoreCase: true, out var parsed)
                ? parsed
                : DiagramDataFamily.Chart, out var fb) ? fb : "?";
        SilentFallback?.Invoke(
            $"DS0582 bundle.renderer_missing: the '{stack}' stack routes {(kindId is null ? family : $"kind '{kindId}'")} " +
            $"to '{missing}', but it is not an active plugin — rendering falls back to '{fallback}'. " +
            $"Add '{missing}' to the runtime bundle.");
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

            if (stack.FamilyPlugins.TryGetValue(family.ToString(), out var pick))
            {
                if (_byId.ContainsKey(pick))
                {
                    return pick;
                }

                AnnounceFallback(stack.Name, family.ToString(), kindId, pick);
            }
        }

        return BuiltInDefaults.TryGetValue(family, out var fallback)
            ? fallback
            : throw new InvalidOperationException($"No viz plugin registered for data family {family}.");
    }
}
