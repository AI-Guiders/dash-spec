using DashSpec.Abstractions.Viz;
using DashSpec.Plugin.Viz.Builtins.Components.Matrix;
using DashSpec.Plugin.Viz.Builtins.Components.MatrixCanvas;

namespace DashSpec.Plugin.Viz.Builtins.Plugins;

public sealed class MatrixCanvasVizDashSpecPlugin : VizDashSpecPluginBase
{
    public override string Id => "viz_matrix_canvas";

    public override string DisplayName => "Matrix canvas renderer";

    protected override string RendererId => VizPluginIds.MatrixCanvas;

    protected override string RendererDisplayName => "Matrix (canvas)";

    protected override Type CardVizComponentType => typeof(MatrixCanvasCardViz);

    protected override Type? CardToolbarComponentType => typeof(MatrixLabelToolbar);

    protected override IVizPlugin CreateBackend() => new MatrixCanvasVizBackend();
}
