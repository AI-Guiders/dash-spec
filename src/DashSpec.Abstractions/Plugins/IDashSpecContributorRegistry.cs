namespace DashSpec.Abstractions.Plugins;

public interface IDashSpecContributorRegistry
{
    void AddDiagramKind(DiagramKindContributorDescriptor descriptor);

    void AddExtensionBlock(ExtensionBlockContributorDescriptor descriptor);

    void AddInteractionHandler(InteractionHandlerDescriptor descriptor);

    void AddActionHandler(ActionHandlerDescriptor descriptor);

    void AddVizRenderer(VizRendererDescriptor descriptor);

    void AddPhraseTemplate(PhraseTemplateDescriptor descriptor);

    void AddScopeContributor(ScopeContributorDescriptor descriptor);

    void AddFilterWidget(FilterWidgetContributorDescriptor descriptor);

    void AddCardChrome(CardChromeContributorDescriptor descriptor);

    void AddCommand(DashSpecCommandDescriptor descriptor);
}

public sealed record FilterWidgetContributorDescriptor(
    string PluginId,
    string WidgetId,
    IReadOnlyList<string> FilterKinds);

public enum CardChromeRenderKind
{
    Buttons,
    ViewSwitch,
}

public sealed record CardChromeContributorDescriptor(
    string PluginId,
    string BlockKeyword,
    CardChromeRenderKind RenderKind);

public sealed record DiagramKindContributorDescriptor(
    string PluginId,
    string KindId,
    string DataFamily,
    bool SupportsTopLimit,
    IReadOnlyList<string> BindingProperties);

public sealed record ExtensionBlockContributorDescriptor(
    string PluginId,
    string BlockKeyword,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> PropertyNames);

public sealed record InteractionHandlerDescriptor(
    string PluginId,
    string HandlerId,
    string DisplayName);

public sealed record ActionHandlerDescriptor(
    string PluginId,
    string ActionId,
    string DisplayName);

public sealed record VizRendererDescriptor(
    string PluginId,
    string RendererId,
    string DataFamily);
