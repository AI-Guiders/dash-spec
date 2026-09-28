using DashSpec.Abstractions.Viz;
using DashSpec.Plugin.Viz.Builtins.Components.CssGrid;
using DashSpec.Plugin.Viz.Builtins.Components.Matrix;

namespace DashSpec.Plugin.Viz.Builtins.Plugins;

public sealed class CssGridVizDashSpecPlugin : VizDashSpecPluginBase
{
    public override string Id => "viz_css_grid";

    public override string DisplayName => "Matrix CSS grid renderer (legacy)";

    protected override string RendererId => VizPluginIds.CssGrid;

    protected override string RendererDisplayName => "Matrix (CSS grid, legacy)";

    protected override Type CardVizComponentType => typeof(CssGridCardViz);

    protected override Type? CardToolbarComponentType => typeof(MatrixLabelToolbar);

    protected override IVizPlugin CreateBackend() => new CssGridVizBackend();
}
