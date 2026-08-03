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

    public IReadOnlyDictionary<string, ModuleDiagramDefinition> ExportDefinitions()
    {
        if (_diagrams.Count == 0)
        {
            return DashboardDocument.EmptyModuleDiagrams;
        }

        return _diagrams.ToDictionary(
            static kv => kv.Key,
            static kv => new ModuleDiagramDefinition(
                kv.Value.Diagram ?? throw new DashSpecParseException($"Diagram include '{kv.Key}' has no kind block."),
                kv.Value.Presentation,
                kv.Value.SeriesTransform),
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
}
