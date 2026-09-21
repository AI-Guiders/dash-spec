using AIGuiders.Surface.Wpf.Abstractions;
using AIGuiders.Surface.Wpf.CodeCenter;
using DashSpec.Modeling.CodeCenter;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;

namespace DashSpec.CodeCenter.Plugin;

static class DashSpecGraphProjection
{
    public static IReadOnlyList<DocumentGraphNodeView> ReadDiagramBoxes(IDocumentSession session)
    {
        var graph = DashSpecProjectionBridge.buildConceptGraph(session.Text);
        var diagramIds = DashSpecProjectionBridge.diagramNodeIds(graph).ToHashSet();
        return DocumentGraphProjection.ReadNodes(session)
            .Where(node => diagramIds.Contains(node.Id))
            .ToArray();
    }

    public static IReadOnlyList<DocumentGraphNodeView> ReadFormFields(IDocumentSession session)
    {
        var graph = DashSpecProjectionBridge.buildConceptGraph(session.Text);
        var formIds = DashSpecProjectionBridge.formFieldNodeIds(graph).ToHashSet();
        return DocumentGraphProjection.ReadNodes(session)
            .Where(node => formIds.Contains(node.Id))
            .ToArray();
    }

    public static string BuildPreviewText(IDocumentSession session) =>
        DashSpecProjectionBridge.buildPreviewOutlineFromText(session.Text);
}
