using AIGuiders.Surface.Wpf.Abstractions;
using AIGuiders.Surface.Wpf.CodeCenter;
using WpfSessionAnchor = AIGuiders.Surface.Wpf.Abstractions.SessionAnchor;

namespace DashSpec.Execution.LanguageEditor;

/// <summary>Federation semantic session for DashSpec (62d+).</summary>
public sealed class DashSpecFederationDocumentSession : FederationCodeCenterSession
{
    public DashSpecFederationDocumentSession(string documentId, string text)
        : base(documentId, text)
    {
    }

    public IReadOnlyList<string> GetGraphCompletions(WpfSessionAnchor anchor) =>
        GetGraphCompletionLabels(anchor.CaretOffset ?? 0);
}
