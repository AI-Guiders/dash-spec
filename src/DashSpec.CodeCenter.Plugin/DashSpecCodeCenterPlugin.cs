using AIGuiders.Platform.Modeling.CodeCenter;
using AIGuiders.Surface.Wpf.Abstractions;
using AIGuiders.Surface.Wpf.CodeCenter;

namespace DashSpec.CodeCenter.Plugin;

/// <summary>DashSpec planet bundle for federation Code Center (ship-63 gate).</summary>
public sealed class DashSpecCodeCenterPlugin : ICodeCenterPlugin
{
    public string PluginId => "dashspec.codecenter";

    public void RegisterProjectionPlugins(ICodeCenterProjectionRegistry registry)
    {
        registry.Register(ProjectionKind.Tree, session => new TreeProjectionSurface(session));
        registry.Register(ProjectionKind.Diagram, session => new DiagramProjectionSurface(session));
        registry.Register(ProjectionKind.Form, session => new FormProjectionSurface(session));
        registry.Register(ProjectionKind.Preview, session => new PreviewProjectionSurface(session));
    }

    public void RegisterTheme(ICodeCenterThemeRegistry registry)
    {
        registry.RegisterClassificationBrush("@dashboard", "DashSpec.Dashboard");
        registry.RegisterClassificationBrush("tab", "DashSpec.Tab");
    }

    public void RegisterCommandContributions(ICodeCenterCommandRegistry registry)
    {
        registry.RegisterStructuralCommand("insert-tab", (session, anchor) =>
        {
            if (session is not FederationCodeCenterSession federation)
            {
                return false;
            }

            if (!session.TryResolveAnchor(anchor, out var locus) || string.IsNullOrWhiteSpace(locus.SymbolId))
            {
                return false;
            }

            var snapshot = DocumentGraph.rebuildFromText(session.Text);
            var node = snapshot.Nodes.Values.FirstOrDefault(n => DocumentGraph.formatNodeId(n.Id) == locus.SymbolId);
            if (node is null)
            {
                return false;
            }

            var edit = StructuralEditBridge.insertBlock(node.Id, "tab", "newTab as \"New\"");
            return federation.TryApplyStructural(edit);
        });
    }

    public void RegisterArgSuggestionProviders(ICodeCenterArgSuggestionRegistry registry)
    {
        registry.RegisterProvider("dashspec", (session, _) =>
            session.GetClassificationSpans().Select(s => s.Kind).Distinct().ToArray());
    }

    public void RegisterLanguageBackends(ICodeCenterLanguageBackendRegistry registry) =>
        registry.Register("dashspec", new DashSpecLanguageBackend());
}

public sealed class DashSpecLanguageBackend
{
    public string LanguageId => "dashspec";
}

public static class CodeCenterPluginBootstrap
{
    public static CodeCenterPluginRuntime CreateRuntime() =>
        CodeCenterPluginHostLoader.LoadPlugins(
            AppContext.BaseDirectory,
            [new DashSpecCodeCenterPlugin()]);
}
