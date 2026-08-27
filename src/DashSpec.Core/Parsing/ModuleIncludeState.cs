using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal enum DocumentModuleKind
{
    Dashboard,
    Tab,
}

internal sealed class ModuleIncludeState
{
    private readonly Dictionary<string, SpecIncludeFragment> _diagrams =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, PresentationBlock> _chartChromePresets =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, TooltipDefinition> _tooltips =
        new(StringComparer.OrdinalIgnoreCase);

    public LayoutBoardDefinition? LayoutBoard { get; private set; }

    public LayoutBoardDefinition? ToolbarBoard { get; private set; }

    public bool TryGetDiagram(string id, out SpecIncludeFragment fragment) =>
        _diagrams.TryGetValue(id, out fragment!);

    public void RegisterDiagram(string id, SpecIncludeFragment fragment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (_diagrams.ContainsKey(id))
        {
            throw new DashSpecParseException($"Duplicate diagram id '{id}' in module includes.");
        }

        _diagrams[id] = fragment;
    }

    public void RegisterTooltip(string id, TooltipDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(definition);
        if (_tooltips.ContainsKey(id))
        {
            throw new DashSpecParseException($"Duplicate tooltip id '{id}' in module includes.");
        }

        _tooltips[id] = definition;
    }

    public void RegisterChartChromePreset(string id, PresentationBlock block)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(block);
        if (_chartChromePresets.ContainsKey(id))
        {
            throw new DashSpecParseException($"Duplicate chart chrome preset '{id}' in module includes.");
        }

        _chartChromePresets[id] = block;
    }

    public IReadOnlyDictionary<string, PresentationBlock> ExportChartChromePresets()
    {
        if (_chartChromePresets.Count == 0)
        {
            return DashboardDocument.EmptyModuleChartChromePresets;
        }

        return new Dictionary<string, PresentationBlock>(_chartChromePresets, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, TooltipDefinition> ExportTooltips()
    {
        if (_tooltips.Count == 0)
        {
            return DashboardDocument.EmptyModuleTooltips;
        }

        return new Dictionary<string, TooltipDefinition>(_tooltips, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, ModuleDiagramDefinition> ExportDefinitions()
    {
        if (_diagrams.Count == 0)
        {
            return DashboardDocument.EmptyModuleDiagrams;
        }

        return _diagrams.ToDictionary(
            static kv => kv.Key,
            kv =>
            {
                var fragment = kv.Value;
                var tooltips = MergeTooltipMaps(_tooltips, fragment.Tooltips);
                TooltipDefinition? tooltip = null;
                if (fragment.Inspect?.TooltipId is { } tooltipId &&
                    tooltips.TryGetValue(tooltipId, out var resolved))
                {
                    tooltip = resolved;
                }

                return new ModuleDiagramDefinition(
                    fragment.Diagram ?? throw new DashSpecParseException($"Diagram include '{kv.Key}' has no kind block."),
                    fragment.Presentation,
                    fragment.SeriesTransform,
                    fragment.Inspect,
                    tooltip);
            },
            StringComparer.OrdinalIgnoreCase);
    }

    public void AssignLayoutBoard(LayoutBoardDefinition board, string context)
    {
        if (LayoutBoard is not null)
        {
            throw new DashSpecParseException($"{context} declares more than one card layout board.");
        }

        LayoutModuleScopeValidator.EnsureMatchesIncludeSite(board, LayoutScope.Tab, context);
        LayoutBoard = board;
    }

    public void AssignToolbarBoard(LayoutBoardDefinition board, string context)
    {
        if (ToolbarBoard is not null)
        {
            throw new DashSpecParseException($"{context} declares more than one toolbar layout board.");
        }

        LayoutModuleScopeValidator.EnsureMatchesIncludeSite(board, LayoutScope.Toolbar, context);
        ToolbarBoard = board;
    }

    private static Dictionary<string, TooltipDefinition> MergeTooltipMaps(
        IReadOnlyDictionary<string, TooltipDefinition> stateTooltips,
        IReadOnlyDictionary<string, TooltipDefinition>? fragmentTooltips)
    {
        var merged = new Dictionary<string, TooltipDefinition>(stateTooltips, StringComparer.OrdinalIgnoreCase);
        if (fragmentTooltips is null)
        {
            return merged;
        }

        foreach (var (key, value) in fragmentTooltips)
        {
            merged[key] = value;
        }

        return merged;
    }
}
