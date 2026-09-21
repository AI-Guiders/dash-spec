using AIGuiders.Platform.Modeling.CodeCenter;
using AIGuiders.Surface.Wpf.Abstractions;
using AIGuiders.Surface.Wpf.CodeCenter;
using AIGuiders.Surface.Wpf.CodeCenter.Plugins;
using DashSpec.Modeling.CodeCenter;

namespace DashSpec.CodeCenter.Plugin;

/// <summary>DashSpec planet bundle for federation Code Center (ship-63 gate).</summary>
public sealed class DashSpecCodeCenterPlugin : ICodeCenterPlugin
{
    public string Id => "dashspec.codecenter";

    public void RegisterProjectionPlugins(ICodeCenterProjectionRegistry registry)
    {
        registry.Register("dashspec.diagram", ProjectionKind.Diagram, _ => new DashSpecDiagramProjectionContribution());
        registry.Register("dashspec.form", ProjectionKind.Form, _ => new DashSpecFormProjectionContribution());
        registry.Register("dashspec.preview", ProjectionKind.Preview, _ => new DashSpecPreviewProjectionContribution());
    }

    public void RegisterTheme(ICodeCenterThemeRegistry registry)
    {
        registry.RegisterBrush("DashSpec.Dashboard", 0xC5, 0x86, 0xC0);
        registry.RegisterBrush("DashSpec.Tab", 0x56, 0x9C, 0xD6);
        registry.RegisterBrush("DashSpec.End", 0x56, 0x9C, 0xD6);
        registry.RegisterClassificationBrush("ModuleHeader", "DashSpec.Dashboard");
        registry.RegisterClassificationBrush("@dashboard", "DashSpec.Dashboard");
        registry.RegisterClassificationBrush("tab", "DashSpec.Tab");
        registry.RegisterClassificationBrush("end", "DashSpec.End");
    }

    public void RegisterCommandContributions(ICodeCenterCommandRegistry registry)
    {
        registry.RegisterStructuralCommand("insert-tab", (session, anchor) =>
        {
            if (session is not FederationCodeCenterSession federation)
            {
                return false;
            }

            if (!federation.TryResolveNodeId(anchor.CaretOffset ?? 0, out var nodeId))
            {
                return false;
            }

            var edit = StructuralEditBridge.insertBlock(nodeId, "tab newTab as \"New\"");
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
            [
                ..CodeCenterCorePluginBootstrap.CorePlugins,
                new DashSpecCodeCenterPlugin()
            ]);
}
