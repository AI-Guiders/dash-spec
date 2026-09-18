using AIGuiders.Surface.Wpf.CodeCenter;
using DashSpec.Modeling.CodeCenter;
using WpfSessionAnchor = AIGuiders.Surface.Wpf.Abstractions.SessionAnchor;

namespace DashSpec.Execution.LanguageEditor;

/// <summary>Federation semantic session for DashSpec (62d+) on full SyntaxTree graph.</summary>
public sealed class DashSpecFederationDocumentSession : FederationCodeCenterSession
{
    public DashSpecFederationDocumentSession(string documentId, string text)
        : base(DashSpecCodeCenterSession.createDocumentSession(documentId, text))
    {
    }

    public IReadOnlyList<string> GetGraphCompletions(WpfSessionAnchor anchor) =>
        GetGraphCompletionLabels(anchor.CaretOffset ?? 0);
}
