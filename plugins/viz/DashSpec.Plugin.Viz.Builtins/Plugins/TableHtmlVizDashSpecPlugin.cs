using DashSpec.Abstractions.Plugins;
using DashSpec.Abstractions.Viz;
using DashSpec.Plugin.Viz.Builtins.Components.TableHtml;

namespace DashSpec.Plugin.Viz.Builtins.Plugins;

public sealed class TableHtmlVizDashSpecPlugin : VizDashSpecPluginBase
{
    public override string Id => "viz_table_html";

    public override string DisplayName => "HTML table renderer";

    protected override string RendererId => VizPluginIds.TableHtml;

    protected override string RendererDisplayName => "Table";

    protected override Type CardVizComponentType => typeof(TableHtmlCardViz);

    protected override IVizPlugin CreateBackend() => new TableHtmlVizBackend();

    public override void RegisterContributors(IDashSpecContributorRegistry registry)
    {
        base.RegisterContributors(registry);

        // ADR-0107: the click-source vocabulary this diagram family renders.
        registry.AddClickSource(new ClickSourceContributorDescriptor(Id, TableClickSources.Row));
    }
}
