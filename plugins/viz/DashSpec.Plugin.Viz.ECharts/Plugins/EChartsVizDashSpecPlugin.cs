using DashSpec.Abstractions.Plugins;
using DashSpec.Abstractions.Viz;
using DashSpec.Plugin.Viz.Builtins.Components.Matrix;
using DashSpec.Plugin.Viz.EChartsUI.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Plugin.Viz.EChartsUI.Plugins;

/// <summary>
/// ECharts matrix renderer (ADR-0112 "чистый JS" branch, spike): a guest renderer for the
/// matrix family — square-cell heatmaps over the matrix payload, Apache ECharts under the
/// hood. Stacks delegate the <c>heatmap</c> kind here (KindPlugins); the author never
/// picks a renderer.
/// </summary>
public sealed class EChartsVizDashSpecPlugin : VizDashSpecPluginBase
{
    public const string RendererIdValue = "echarts";

    public override string Id => "viz_echarts";

    public override string DisplayName => "ECharts renderer (matrix guest)";

    protected override string RendererId => RendererIdValue;

    protected override string RendererDisplayName => "ECharts";

    protected override Type CardVizComponentType => typeof(EChartsCardViz);

    /// <summary>The matrix display toolbar (D/X/Y label glyphs) — the same family-semantics
    /// widget the builtin and Radzen stacks use; stack-agnostic by design.</summary>
    protected override Type? CardToolbarComponentType => typeof(MatrixLabelToolbar);

    protected override IVizPlugin CreateBackend() => new EChartsVizBackend();

    private sealed class EChartsVizBackend : IVizPlugin
    {
        public string Id => RendererIdValue;

        public string DataFamily => "matrix";
    }
}
