using System.Globalization;

namespace DashSpec.Execution.Runtime;

/// <summary>Named presets from <c>@diagramlibrary</c> TOML (presentation, transform, diagram, card).</summary>
public sealed class SpecLibrary
{
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _presentations =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, SeriesTransformPreset> _seriesTransforms =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, DiagramPreset> _diagrams =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, CardPreset> _cards =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _palettes =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string>? TryGetPresentation(string name) =>
        _presentations.TryGetValue(name, out var preset) ? preset : null;

    public SeriesTransformPreset? TryGetSeriesTransform(string name) =>
        _seriesTransforms.TryGetValue(name, out var preset) ? preset : null;

    public DiagramPreset? TryGetDiagram(string name) =>
        _diagrams.TryGetValue(name, out var preset) ? preset : null;

    public CardPreset? TryGetCard(string name) =>
        _cards.TryGetValue(name, out var preset) ? preset : null;

    public IReadOnlyDictionary<string, string>? TryGetPalette(string name) =>
        _palettes.TryGetValue(name, out var preset) ? preset : null;

    public static SpecLibrary FromPalette(string id, IReadOnlyDictionary<string, string> properties)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(properties);

        var library = new SpecLibrary();
        library.RegisterPalettePreset(id, properties);
        return library;
    }

    public static SpecLibrary? Merge(SpecLibrary? left, SpecLibrary? right)
    {
        if (left is null)
        {
            return right;
        }

        if (right is null)
        {
            return left;
        }

        var merged = new SpecLibrary();
        merged.ImportFrom(left);
        merged.ImportFrom(right);
        return merged;
    }

    public static SpecLibrary FromModuleDefinitions(IReadOnlyDictionary<string, ModuleDiagramDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var library = new SpecLibrary();
        foreach (var (id, definition) in definitions)
        {
            library.ImportModuleDefinition(id, definition);
        }

        return library;
    }

    public static SpecLibrary FromChartChromePresets(IReadOnlyDictionary<string, PresentationBlock> presets)
    {
        ArgumentNullException.ThrowIfNull(presets);

        var library = new SpecLibrary();
        foreach (var (id, block) in presets)
        {
            library.RegisterPresentationBlock(id, block);
        }

        return library;
    }

    public static SpecLibrary FromModuleDocument(
        IReadOnlyDictionary<string, PresentationBlock> chartChromePresets,
        IReadOnlyDictionary<string, ModuleDiagramDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(chartChromePresets);
        ArgumentNullException.ThrowIfNull(definitions);

        var library = FromChartChromePresets(chartChromePresets);
        foreach (var (id, definition) in definitions)
        {
            library.ImportModuleDefinition(id, definition);
        }

        return library;
    }

    public void ImportModuleDefinition(string id, ModuleDiagramDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(definition);

        var diagram = definition.Diagram;
        if (string.IsNullOrWhiteSpace(diagram.Kind))
        {
            throw new DashSpecParseException($"Module diagram '{id}' has no chart kind.");
        }

        diagram.Properties.TryGetValue("render", out var render);
        var diagramProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in diagram.Properties)
        {
            if (!string.Equals(key, "render", StringComparison.OrdinalIgnoreCase))
            {
                diagramProps[key] = value;
            }
        }

        string? presentationPreset = null;
        if (definition.Presentation?.Value is { } presentation)
        {
            var usePreset = presentation.UsePreset?.Value;
            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(usePreset))
            {
                if (_presentations.TryGetValue(usePreset, out var parent))
                {
                    foreach (var (key, value) in parent)
                    {
                        merged[key] = value;
                    }
                }
            }

            foreach (var (key, value) in presentation.Properties)
            {
                if (!string.Equals(key, "use", StringComparison.OrdinalIgnoreCase))
                {
                    merged[key] = value;
                }
            }

            if (merged.Count > 0)
            {
                var presetId = $"{id}__presentation";
                _presentations[presetId] = merged;
                presentationPreset = presetId;
            }
            else if (!string.IsNullOrWhiteSpace(usePreset))
            {
                presentationPreset = usePreset;
            }
        }

        string? seriesTransformPreset = null;
        if (definition.SeriesTransform?.Value is { } seriesTransform)
        {
            var usePreset = seriesTransform.UsePreset?.Value;
            if (!string.IsNullOrWhiteSpace(usePreset))
            {
                seriesTransformPreset = usePreset;
            }
            else if (seriesTransform.Max?.Value is int max && max > 0)
            {
                seriesTransformPreset = $"{id}__series";
                _seriesTransforms[seriesTransformPreset] = new SeriesTransformPreset(
                    max,
                    seriesTransform.OtherLabel?.Value ?? "Other");
            }
        }

        if (_diagrams.ContainsKey(id))
        {
            throw new DashSpecParseException($"Duplicate module diagram preset '{id}'.");
        }

        _diagrams[id] = new DiagramPreset(
            diagram.Kind,
            string.IsNullOrWhiteSpace(render) ? null : render,
            presentationPreset,
            seriesTransformPreset,
            diagramProps,
            definition.Inspect?.Value,
            definition.Tooltip?.Value);
    }

    private void RegisterPresentationBlock(string name, PresentationBlock? block)
    {
        if (block is null)
        {
            return;
        }

        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var usePreset = block.UsePreset?.Value;
        if (!string.IsNullOrWhiteSpace(usePreset))
        {
            if (_presentations.TryGetValue(usePreset, out var parent))
            {
                foreach (var (key, value) in parent)
                {
                    merged[key] = value;
                }
            }
        }

        foreach (var (key, value) in block.Properties)
        {
            if (!string.Equals(key, "use", StringComparison.OrdinalIgnoreCase))
            {
                merged[key] = value;
            }
        }

        if (merged.Count > 0)
        {
            _presentations[name] = merged;
        }
    }

    private void ImportFrom(SpecLibrary other)
    {
        foreach (var (key, value) in other._palettes)
        {
            _palettes[key] = value;
        }

        foreach (var (key, value) in other._presentations)
        {
            _presentations[key] = value;
        }

        foreach (var (key, value) in other._seriesTransforms)
        {
            _seriesTransforms[key] = value;
        }

        foreach (var (key, value) in other._diagrams)
        {
            _diagrams[key] = value;
        }

        foreach (var (key, value) in other._cards)
        {
            _cards[key] = value;
        }
    }

    // Loader-side registration (internal for SpecLibraryLoader in Execution.Core; IVT).
    internal void RegisterPresentationPreset(string id, IReadOnlyDictionary<string, string> props) =>
        _presentations[id] = new Dictionary<string, string>(props, StringComparer.OrdinalIgnoreCase);

    internal void RegisterSeriesTransformPreset(string id, SeriesTransformPreset preset) =>
        _seriesTransforms[id] = preset;

    internal void RegisterDiagramPreset(string id, DiagramPreset preset) => _diagrams[id] = preset;

    internal void RegisterCardPreset(string id, CardPreset preset) => _cards[id] = preset;

    internal void RegisterPalettePreset(string id, IReadOnlyDictionary<string, string> props) =>
        _palettes[id] = new Dictionary<string, string>(props, StringComparer.OrdinalIgnoreCase);
}

public sealed record SeriesTransformPreset(int Max, string OtherLabel);

public sealed record DiagramPreset(
    string Kind,
    string? Render,
    string? PresentationPreset,
    string? SeriesTransformPreset,
    IReadOnlyDictionary<string, string> Properties,
    InspectPresentation? Inspect = null,
    TooltipDefinition? Tooltip = null);

public sealed record CardPreset(
    string DiagramPreset,
    DataSourceDefinition DataSource,
    IReadOnlyList<string> BindFilters);
