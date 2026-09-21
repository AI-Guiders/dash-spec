using AIGuiders.Surface.Wpf.Abstractions;
using AIGuiders.Surface.Wpf.CodeCenter;
using DashSpec.Modeling.CodeCenter;

namespace DashSpec.CodeCenter.Plugin;

static class DashSpecGraphProjection
{
    public static IReadOnlyList<DocumentGraphNodeView> ReadDiagramBoxes(IDocumentSession session) =>
        DocumentGraphProjection.ReadNodes(session)
            .Where(node => DashSpecProjectionBridge.isDiagramBox(node.Name))
            .ToArray();

    public static IReadOnlyList<DocumentGraphNodeView> ReadFormFields(IDocumentSession session) =>
        DocumentGraphProjection.ReadNodes(session)
            .Where(node => DashSpecProjectionBridge.isFormField(node.Name))
            .ToArray();

    public static string BuildPreviewText(IDocumentSession session) =>
        DashSpecProjectionBridge.buildPreviewOutlineFromText(session.Text);
}
