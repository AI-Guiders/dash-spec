using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using DashSpec.Host.Plugins;
using DashSpec.Host.Services.Models;
using DashSpec.Host.Services.Presentation;

namespace DashSpec.Host.Services.Rendering;

public static class CardRenderSkeletonFactory
{
    public static CardRenderResult CreateLoading(
        CardDefinition card,
        SpecLibrary? library,
        VizPluginRegistry vizPlugins,
        IReadOnlyList<string> dashboardFilters,
        DashboardDocument? document = null)
    {
        var resolved = CardResolver.Resolve(card, library, dashboardFilters);
        var effective = resolved.Card;
        var kind = DiagramKindRegistry.Resolve(effective.Diagram.Kind);
        var renderPluginId = vizPlugins.Resolve(resolved.RenderPluginId, kind.DataFamily);
        var interiorPlacements = document is not null && card.InteriorBoard is not null
            ? DashboardLayoutHelper.ResolveInteriorPlacements(card, document)
            : null;
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
            InteriorPlacements: interiorPlacements,
            ChartPresentation: kind.DataFamily is DiagramDataFamily.Chart
                ? CardChromeResolver.ResolveChartPresentation(effective, library)
                : null,
            MatrixPresentation: kind.DataFamily is DiagramDataFamily.Matrix
                ? MatrixPresentation.FromCard(effective, library)
                : null,
            ClickBehaviour: card.ClickBehaviour,
            ExtensionBlocks: card.ExtensionBlocks,
            LocalFiltersManualApply: card.LocalFiltersManualApply,
            MatrixLimits: card.MatrixLimits,
            OversizeMessage: card.OversizeMessage);
    }

    public static CardRenderResult CreatePlaceholder(
        CardDefinition card,
        SpecLibrary? library,
        VizPluginRegistry vizPlugins,
        IReadOnlyList<string> dashboardFilters,
        string message,
        DashboardDocument? document = null)
    {
        var loading = CreateLoading(card, library, vizPlugins, dashboardFilters, document);
        return loading with
        {
            Loading = false,
            IsVisibilityPlaceholder = true,
            VisibilityMessage = message,
        };
    }

    public static CardRenderResult CreateError(
        CardDefinition card,
        SpecLibrary? library,
        VizPluginRegistry vizPlugins,
        IReadOnlyList<string> dashboardFilters,
        string error,
        DashboardDocument? document = null)
    {
        var resolved = CardResolver.Resolve(card, library, dashboardFilters);
        var effective = resolved.Card;
        var kind = DiagramKindRegistry.Resolve(effective.Diagram.Kind);
        var renderPluginId = vizPlugins.Resolve(resolved.RenderPluginId, kind.DataFamily);
        var interiorPlacements = document is not null && card.InteriorBoard is not null
            ? DashboardLayoutHelper.ResolveInteriorPlacements(card, document)
            : null;
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
            InteriorPlacements: interiorPlacements,
            ChartPresentation: kind.DataFamily is DiagramDataFamily.Chart
                ? CardChromeResolver.ResolveChartPresentation(effective, library)
                : null,
            MatrixPresentation: kind.DataFamily is DiagramDataFamily.Matrix
                ? MatrixPresentation.FromCard(effective, library)
                : null,
            ClickBehaviour: card.ClickBehaviour,
            ExtensionBlocks: card.ExtensionBlocks,
            LocalFiltersManualApply: card.LocalFiltersManualApply,
            MatrixLimits: card.MatrixLimits,
            OversizeMessage: card.OversizeMessage);
    }
}
