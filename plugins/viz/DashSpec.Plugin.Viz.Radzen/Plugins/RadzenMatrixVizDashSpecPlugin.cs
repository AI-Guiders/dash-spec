using DashSpec.Abstractions.Plugins;
using DashSpec.Abstractions.Viz;
using DashSpec.Plugin.Viz.Builtins.Components.Matrix;
using DashSpec.Plugin.Viz.RadzenUI.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Plugin.Viz.RadzenUI.Plugins;

/// <summary>
/// ADR-0111: the Radzen stack's own matrix renderer — RadzenHeatmapSeries over the
/// matrix payload (12.1.11 native), no JS bridge. The stack routes the heatmap kind
/// here (KindPlugins); the ECharts guest stays for stacks that prefer it.
/// </summary>
public sealed class RadzenMatrixVizDashSpecPlugin : VizDashSpecPluginBase
{
    public const string RendererIdValue = "radzen_heatmap";

    public override string Id => "viz_radzen_matrix";

    public override string DisplayName => "Radzen heatmap renderer";

    protected override string RendererId => RendererIdValue;

    protected override string RendererDisplayName => "Radzen Heatmap";

    protected override Type CardVizComponentType => typeof(RadzenHeatmapCardViz);

    /// <summary>The same family-semantics toolbar the other matrix renderers use —
    /// its buttons bind to the stack through the chrome seam.</summary>
    protected override Type? CardToolbarComponentType => typeof(MatrixLabelToolbar);

    protected override IVizPlugin CreateBackend() => new RadzenHeatmapBackend();

    private sealed class RadzenHeatmapBackend : IVizPlugin
    {
        public string Id => RendererIdValue;

        public string DataFamily => "matrix";
    }
}
