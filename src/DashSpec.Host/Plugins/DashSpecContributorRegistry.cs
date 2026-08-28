using DashSpec.Abstractions.Plugins;

namespace DashSpec.Host.Plugins;

public sealed class DashSpecContributorRegistry : IDashSpecContributorRegistry
{
    private readonly List<LoadedPluginEntry> _plugins = [];
    private readonly Dictionary<string, DiagramKindContributorDescriptor> _diagramKinds =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ExtensionBlockContributorDescriptor> _extensionBlocks =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, InteractionHandlerDescriptor> _interactionHandlers =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ActionHandlerDescriptor> _actionHandlers =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, VizRendererDescriptor> _vizRenderers =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PhraseTemplateDescriptor> _phraseTemplates =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ScopeContributorDescriptor> _scopeContributors =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FilterWidgetContributorDescriptor> _filterWidgets =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CardChromeContributorDescriptor> _cardChrome =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IDashSpecEndpointContributor> _endpointContributors = [];
    private readonly List<DashSpecCommandDescriptor> _commands = [];

    public IReadOnlyList<IDashSpecEndpointContributor> EndpointContributors => _endpointContributors;

    public IReadOnlyList<DashSpecCommandDescriptor> CommandDescriptors => _commands;

    public bool ContainsPlugin(string pluginId) =>
        _plugins.Any(x => string.Equals(x.Id, pluginId, StringComparison.OrdinalIgnoreCase));

    public void RegisterPlugin(IDashSpecPlugin plugin)
    {
        if (ContainsPlugin(plugin.Id))
        {
            throw new InvalidOperationException($"Duplicate plugin id '{plugin.Id}'.");
        }

        _plugins.Add(new LoadedPluginEntry(plugin.Id, plugin.DisplayName, plugin.Tier));
        plugin.RegisterContributors(this);

        if (plugin is IDashSpecEndpointContributor endpointContributor)
        {
            _endpointContributors.Add(endpointContributor);
        }
    }

    public void AddDiagramKind(DiagramKindContributorDescriptor descriptor)
    {
        if (!_diagramKinds.TryAdd(descriptor.KindId, descriptor))
        {
            throw new InvalidOperationException(
                $"Duplicate diagram kind '{descriptor.KindId}' from plugin '{descriptor.PluginId}'.");
        }

        TrackPlugin(descriptor.PluginId, diagramKind: descriptor.KindId);
    }

    public void AddExtensionBlock(ExtensionBlockContributorDescriptor descriptor)
    {
        if (!_extensionBlocks.TryAdd(descriptor.BlockKeyword, descriptor))
        {
            throw new InvalidOperationException(
                $"Duplicate extension block '{descriptor.BlockKeyword}' from plugin '{descriptor.PluginId}'.");
        }

        TrackPlugin(descriptor.PluginId, extensionBlock: descriptor.BlockKeyword);
    }

    public void AddInteractionHandler(InteractionHandlerDescriptor descriptor)
    {
        if (!_interactionHandlers.TryAdd(descriptor.HandlerId, descriptor))
        {
            throw new InvalidOperationException(
                $"Duplicate interaction handler '{descriptor.HandlerId}' from plugin '{descriptor.PluginId}'.");
        }

        TrackPlugin(descriptor.PluginId, interactionHandler: descriptor.HandlerId);
    }

    public void AddActionHandler(ActionHandlerDescriptor descriptor)
    {
        if (!_actionHandlers.TryAdd(descriptor.ActionId, descriptor))
        {
            throw new InvalidOperationException(
                $"Duplicate action handler '{descriptor.ActionId}' from plugin '{descriptor.PluginId}'.");
        }

        TrackPlugin(descriptor.PluginId, actionHandler: descriptor.ActionId);
    }

    public void AddVizRenderer(VizRendererDescriptor descriptor)
    {
        if (!_vizRenderers.TryAdd(descriptor.RendererId, descriptor))
        {
            throw new InvalidOperationException(
                $"Duplicate viz renderer '{descriptor.RendererId}' from plugin '{descriptor.PluginId}'.");
        }

        TrackPlugin(descriptor.PluginId);
    }

    public void AddPhraseTemplate(PhraseTemplateDescriptor descriptor)
    {
        var key = $"{descriptor.Scope}::{descriptor.Pattern}";
        if (!_phraseTemplates.TryAdd(key, descriptor))
        {
            throw new InvalidOperationException(
                $"Duplicate phrase template '{descriptor.Pattern}' in scope '{descriptor.Scope}' from plugin '{descriptor.PluginId}'.");
        }

        TrackPlugin(descriptor.PluginId, phraseTemplate: descriptor.Pattern);
    }

    public void AddScopeContributor(ScopeContributorDescriptor descriptor)
    {
        if (!_scopeContributors.TryAdd(descriptor.ScopeId, descriptor))
        {
            throw new InvalidOperationException(
                $"Duplicate scope '{descriptor.ScopeId}' from plugin '{descriptor.PluginId}'.");
        }

        TrackPlugin(descriptor.PluginId);
    }

    public void AddFilterWidget(FilterWidgetContributorDescriptor descriptor)
    {
        if (!_filterWidgets.TryAdd(descriptor.WidgetId, descriptor))
        {
            throw new InvalidOperationException(
                $"Duplicate filter widget '{descriptor.WidgetId}' from plugin '{descriptor.PluginId}'.");
        }

        TrackPlugin(descriptor.PluginId, filterWidget: descriptor.WidgetId);
    }

    public void AddCardChrome(CardChromeContributorDescriptor descriptor)
    {
        if (!_cardChrome.TryAdd(descriptor.BlockKeyword, descriptor))
        {
            throw new InvalidOperationException(
                $"Duplicate card chrome block '{descriptor.BlockKeyword}' from plugin '{descriptor.PluginId}'.");
        }

        TrackPlugin(descriptor.PluginId, cardChrome: descriptor.BlockKeyword);
    }

    public void AddCommand(DashSpecCommandDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (string.IsNullOrWhiteSpace(descriptor.CommandId))
        {
            throw new ArgumentException("CommandId is required.", nameof(descriptor));
        }

        if (_commands.Any(x => string.Equals(x.CommandId, descriptor.CommandId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Duplicate command id '{descriptor.CommandId}' from plugin '{descriptor.PluginId}'.");
        }

        _commands.Add(descriptor);
        TrackPlugin(descriptor.PluginId);
    }

    public IReadOnlyDictionary<string, CardChromeContributorDescriptor> CardChromeBlocks => _cardChrome;

    public IReadOnlyDictionary<string, FilterWidgetContributorDescriptor> FilterWidgets => _filterWidgets;

    public IReadOnlyList<PhraseTemplateDescriptor> PhraseTemplates =>
        _phraseTemplates.Values.ToList();

    public IReadOnlySet<string> ExtensionBlockKeywords =>
        _extensionBlocks.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, ExtensionBlockContributorDescriptor> ExtensionBlocks => _extensionBlocks;

    public IReadOnlyDictionary<string, DiagramKindContributorDescriptor> DiagramKinds => _diagramKinds;

    public IReadOnlyDictionary<string, InteractionHandlerDescriptor> InteractionHandlers => _interactionHandlers;

    public IReadOnlyDictionary<string, ActionHandlerDescriptor> ActionHandlers => _actionHandlers;

    public DashSpecPluginCapabilities BuildCapabilities(string bundle) =>
        new()
        {
            Bundle = bundle,
            DiagramKinds = _diagramKinds.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            ExtensionBlocks = _extensionBlocks.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            InteractionHandlers = _interactionHandlers.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            ActionHandlers = _actionHandlers.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            VizRenderers = _vizRenderers.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            PhraseScopes = _scopeContributors.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            PhraseTemplates = _phraseTemplates.Values
                .Select(x => $"{x.Scope}: {x.Pattern} -> {x.HandlerId}")
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            FilterWidgets = _filterWidgets.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            CardChromeBlocks = _cardChrome.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            Commands = _commands
                .Select(x => new DashSpecCommandCapability
                {
                    PluginId = x.PluginId,
                    CommandId = x.CommandId,
                    Path = x.Path,
                    Help = x.Help,
                    ArgTail = x.ArgTail,
                    PathAliases = x.PathAliases?.ToList() ?? [],
                    Group = x.Group,
                })
                .ToList(),
            Plugins = _plugins
                .Select(entry => new LoadedPluginCapability
                {
                    Id = entry.Id,
                    DisplayName = entry.DisplayName,
                    Tier = entry.Tier.ToString(),
                    DiagramKinds = entry.DiagramKinds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                    ExtensionBlocks = entry.ExtensionBlocks.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                    InteractionHandlers = entry.InteractionHandlers.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                    ActionHandlers = entry.ActionHandlers.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                    PhraseTemplates = entry.PhraseTemplates.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                    FilterWidgets = entry.FilterWidgets.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                    CardChromeBlocks = entry.CardChromeBlocks.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                })
                .ToList(),
        };

    private void TrackPlugin(
        string pluginId,
        string? diagramKind = null,
        string? extensionBlock = null,
        string? interactionHandler = null,
        string? actionHandler = null,
        string? phraseTemplate = null,
        string? filterWidget = null,
        string? cardChrome = null)
    {
        var entry = _plugins.FirstOrDefault(x => string.Equals(x.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return;
        }

        if (diagramKind is not null)
        {
            entry.DiagramKinds.Add(diagramKind);
        }

        if (extensionBlock is not null)
        {
            entry.ExtensionBlocks.Add(extensionBlock);
        }

        if (interactionHandler is not null)
        {
            entry.InteractionHandlers.Add(interactionHandler);
        }

        if (actionHandler is not null)
        {
            entry.ActionHandlers.Add(actionHandler);
        }

        if (phraseTemplate is not null)
        {
            entry.PhraseTemplates.Add(phraseTemplate);
        }

        if (filterWidget is not null)
        {
            entry.FilterWidgets.Add(filterWidget);
        }

        if (cardChrome is not null)
        {
            entry.CardChromeBlocks.Add(cardChrome);
        }
    }

    private sealed class LoadedPluginEntry(string id, string displayName, PluginTier tier)
    {
        public string Id { get; } = id;

        public string DisplayName { get; } = displayName;

        public PluginTier Tier { get; } = tier;

        public HashSet<string> DiagramKinds { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> ExtensionBlocks { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> InteractionHandlers { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> ActionHandlers { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> PhraseTemplates { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> FilterWidgets { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> CardChromeBlocks { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
