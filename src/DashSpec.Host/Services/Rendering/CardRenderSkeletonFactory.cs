using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using DashSpec.Host.Plugins;
using DashSpec.Host.Services.Models;

namespace DashSpec.Host.Services.Rendering;

public static class CardRenderSkeletonFactory
{
    public static CardRenderResult CreateLoading(
        CardDefinition card,
        SpecLibrary? library,
        VizPluginRegistry vizPlugins,
        IReadOnlyList<string> dashboardFilters)
    {
        var resolved = CardResolver.Resolve(card, library, dashboardFilters);
        var effective = resolved.Card;
        var kind = DiagramKindRegistry.Resolve(effective.Diagram.Kind);
        var renderPluginId = vizPlugins.Resolve(resolved.RenderPluginId, kind.DataFamily);
        return new CardRenderResult(
            effective.Id,
            effective.Title,
            effective.Diagram.Kind,
            kind.DataFamily,
            renderPluginId,
            Loading: true,
            BoundFilters: effective.BoundFilters,
            LocalFilters: effective.LocalFilters,
            Placement: effective.Placement,
            ChartPresentation: kind.DataFamily is DiagramDataFamily.Chart
                ? CardChromeResolver.ResolveChartPresentation(effective, library)
                : null,
            MatrixPresentation: kind.DataFamily is DiagramDataFamily.Matrix
                ? MatrixPresentation.FromCard(effective, library)
                : null);
    }

    public static CardRenderResult CreateError(
        CardDefinition card,
        SpecLibrary? library,
        VizPluginRegistry vizPlugins,
        IReadOnlyList<string> dashboardFilters,
        string error)
    {
        var resolved = CardResolver.Resolve(card, library, dashboardFilters);
        var effective = resolved.Card;
        var kind = DiagramKindRegistry.Resolve(effective.Diagram.Kind);
        var renderPluginId = vizPlugins.Resolve(resolved.RenderPluginId, kind.DataFamily);
        return new CardRenderResult(
            effective.Id,
            effective.Title,
            effective.Diagram.Kind,
            kind.DataFamily,
            renderPluginId,
            Error: error,
            BoundFilters: effective.BoundFilters,
            LocalFilters: effective.LocalFilters,
            Placement: effective.Placement,
            ChartPresentation: kind.DataFamily is DiagramDataFamily.Chart
                ? CardChromeResolver.ResolveChartPresentation(effective, library)
                : null,
            MatrixPresentation: kind.DataFamily is DiagramDataFamily.Matrix
                ? MatrixPresentation.FromCard(effective, library)
                : null);
    }
}
