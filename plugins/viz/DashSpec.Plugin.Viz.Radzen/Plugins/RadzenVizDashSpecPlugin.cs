using DashSpec.Abstractions.Viz;
using DashSpec.Plugin.Viz.Builtins.Components.Matrix;
using DashSpec.Plugin.Viz.Builtins.Plugins;
using DashSpec.Plugin.Viz.RadzenUI.Components;
using DashSpec.Plugin.Viz.RadzenUI.Resources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Radzen;

namespace DashSpec.Plugin.Viz.RadzenUI.Plugins;

/// <summary>
/// Radzen Blazor viz plugin ([ADR-0111] visual layer pilot): one MIT package, 100% native C#
/// components — charts (30+ types), Gantt, DataGrid, themes. The pilot renders the line chart
/// family via RadzenChart; the rest of the families follow the same plugin path.
/// </summary>
public sealed class RadzenVizDashSpecPlugin : VizDashSpecPluginBase
{
    public const string RendererIdValue = "radzen";

    public override string Id => "viz_radzen";

    public override string DisplayName => "Radzen renderer";

    protected override string RendererId => RendererIdValue;

    protected override string RendererDisplayName => "Radzen";

    protected override Type CardVizComponentType => typeof(RadzenCardViz);

    /// <summary>The matrix display toolbar (D/X/Y label glyphs) — widget semantics live in
    /// the builtins plugin and are stack-agnostic; the Radzen consumers read the same
    /// ICardVizDisplayState overrides.</summary>
    protected override Type? CardToolbarComponentType => typeof(MatrixLabelToolbar);

    protected override IVizPlugin CreateBackend() => new RadzenVizBackend();

    /// <summary>
    /// Radzen component services (tooltip/dialog/notification): RadzenChart injects
    /// Radzen.TooltipService — without it the component render fails on property injection.
    /// The plugin carries its own services; the host stays out of it.
    /// </summary>
    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);
        services.AddRadzenComponents();
        // Russian chrome for Radzen's built-in strings (ADR-0115 §3): no ru satellites upstream.
        services.AddSingleton<ILocalizer, RadzenRuLocalizer>();
    }

    private sealed class RadzenVizBackend : IVizPlugin
    {
        public string Id => RendererIdValue;

        public string DataFamily => "chart";
    }
}
