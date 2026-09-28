using DashSpec.Abstractions.Viz;
using DashSpec.Plugin.Viz.Builtins.Components.ScalarHtml;

namespace DashSpec.Plugin.Viz.Builtins.Plugins;

public sealed class ScalarHtmlVizDashSpecPlugin : VizDashSpecPluginBase
{
    public override string Id => "viz_scalar_html";

    public override string DisplayName => "Scalar HTML renderer";

    protected override string RendererId => VizPluginIds.ScalarHtml;

    protected override string RendererDisplayName => "Scalar";

    protected override Type CardVizComponentType => typeof(ScalarHtmlCardViz);

    protected override IVizPlugin CreateBackend() => new ScalarHtmlVizBackend();
}
