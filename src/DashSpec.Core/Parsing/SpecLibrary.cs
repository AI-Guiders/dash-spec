using System.Globalization;
using System.Text;
using DashSpec.Core.Model;
using Tomlyn;
using Tomlyn.Model;

namespace DashSpec.Core.Parsing;

/// <summary>Named presets from <c>@diagramlibrary</c> TOML (presentation, transform, diagram, card).</summary>
public sealed class SpecLibrary
{
    private static readonly TomlSerializerOptions TomlOptions = new()
    {
        DottedKeyHandling = TomlDottedKeyHandling.Literal,
    };

    private static readonly HashSet<string> DiagramPresetReservedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "kind",
        "render",
        "presentation",
        "transform.series",
    };

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

    public static SpecLibrary LoadFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Spec library not found: {path}", path);
        }

        return Parse(File.ReadAllText(path, Encoding.UTF8));
    }

    public static SpecLibrary Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        TomlTable root;
        try
        {
            root = TomlSerializer.Deserialize<TomlTable>(text, TomlOptions)
                ?? throw new DashSpecParseException("Spec library: empty TOML document.");
        }
        catch (TomlException ex)
        {
            throw new DashSpecParseException($"Spec library TOML: {ex.Message}");
        }

        var library = new SpecLibrary();
        VisitTable(root, string.Empty, library);
        return library;
    }

    public static SpecLibrary Parse(IReadOnlyList<string> lines) =>
        Parse(string.Join('\n', lines));

    public static SpecLibrary FromPalette(string id, IReadOnlyDictionary<string, string> properties)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(properties);

        var library = new SpecLibrary();
        library._palettes[id] = new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase);
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
        if (definition.Presentation is { } presentation)
        {
            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(presentation.UsePreset))
            {
                EnsureStdlibPresentation(presentation.UsePreset);
                if (_presentations.TryGetValue(presentation.UsePreset, out var parent))
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
            else if (!string.IsNullOrWhiteSpace(presentation.UsePreset))
            {
                presentationPreset = presentation.UsePreset;
            }
        }

        string? seriesTransformPreset = null;
        if (definition.SeriesTransform is { } seriesTransform)
        {
            if (!string.IsNullOrWhiteSpace(seriesTransform.UsePreset))
            {
                seriesTransformPreset = seriesTransform.UsePreset;
            }
            else if (seriesTransform.Max is int max && max > 0)
            {
                seriesTransformPreset = $"{id}__series";
                _seriesTransforms[seriesTransformPreset] = new SeriesTransformPreset(
                    max,
                    seriesTransform.OtherLabel ?? "Other");
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
            definition.Inspect,
            definition.Tooltip);
    }

    private void EnsureStdlibPresentation(string name)
    {
        if (_presentations.ContainsKey(name))
        {
            return;
        }

        var fragment = SpecIncludeResolver.Load("presentation", $"<presentation/{name}>", ".");
        RegisterPresentationBlock(name, fragment.Presentation);
    }

    private void RegisterPresentationBlock(string name, PresentationBlock? block)
    {
        if (block is null)
        {
            return;
        }

        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(block.UsePreset))
        {
            EnsureStdlibPresentation(block.UsePreset);
            if (_presentations.TryGetValue(block.UsePreset, out var parent))
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

    private static void VisitTable(TomlTable table, string path, SpecLibrary library)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var nested = new List<(string Key, TomlTable Child)>();

        foreach (var key in table.Keys)
        {
            var value = table[key];
            switch (value)
            {
                case TomlTable child:
                    nested.Add((key, child));
                    break;
                case TomlTableArray:
                    throw new DashSpecParseException(
                        $"Spec library section '{FormatPath(path)}': table arrays are not supported.");
                case TomlArray:
                    throw new DashSpecParseException(
                        $"Spec library section '{FormatPath(path)}': inline arrays are not supported.");
                default:
                    props[key] = ScalarToString(value);
                    break;
            }
        }

        if (props.Count > 0 && IsRegisteredSectionPath(path))
        {
            RegisterSection(library, path, props);
        }

        foreach (var (key, child) in nested)
        {
            var childPath = string.IsNullOrEmpty(path) ? key : $"{path}.{key}";
            VisitTable(child, childPath, library);
        }
    }

    private static bool IsRegisteredSectionPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var parts = path.Split('.');
        return parts.Length switch
        {
            2 when parts[0] is "presentation" or "diagram" or "card" or "palette" => true,
            3 when parts[0] == "transform" && parts[1] == "series" => true,
            _ => false,
        };
    }

    private static void RegisterSection(
        SpecLibrary library,
        string section,
        IReadOnlyDictionary<string, string> props)
    {
        if (section.StartsWith("presentation.", StringComparison.OrdinalIgnoreCase))
        {
            var id = section["presentation.".Length..];
            library._presentations[id] = new Dictionary<string, string>(props, StringComparer.OrdinalIgnoreCase);
            return;
        }

        if (section.StartsWith("transform.series.", StringComparison.OrdinalIgnoreCase))
        {
            var id = section["transform.series.".Length..];
            library._seriesTransforms[id] = ParseSeriesTransformPreset(props, id);
            return;
        }

        if (section.StartsWith("diagram.", StringComparison.OrdinalIgnoreCase))
        {
            var id = section["diagram.".Length..];
            library._diagrams[id] = ParseDiagramPreset(props, id);
            return;
        }

        if (section.StartsWith("card.", StringComparison.OrdinalIgnoreCase))
        {
            var id = section["card.".Length..];
            library._cards[id] = ParseCardPreset(props, id);
            return;
        }

        if (section.StartsWith("palette.", StringComparison.OrdinalIgnoreCase))
        {
            var id = section["palette.".Length..];
            library._palettes[id] = new Dictionary<string, string>(props, StringComparer.OrdinalIgnoreCase);
            return;
        }

        throw new DashSpecParseException($"Spec library: unknown section '[{section}]'.");
    }

    private static string FormatPath(string path) =>
        string.IsNullOrEmpty(path) ? "(root)" : path;

    private static string ScalarToString(object? value) =>
        value switch
        {
            null => string.Empty,
            string s => s,
            bool b => b ? "true" : "false",
            TomlDateTime dt => dt.ToString(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty,
        };

    private static CardPreset ParseCardPreset(IReadOnlyDictionary<string, string> props, string id)
    {
        if (!props.TryGetValue("diagram", out var diagram) || string.IsNullOrWhiteSpace(diagram))
        {
            throw new DashSpecParseException($"card.{id}: 'diagram' is required.");
        }

        if (!props.TryGetValue("datasource", out var datasource) || string.IsNullOrWhiteSpace(datasource))
        {
            throw new DashSpecParseException($"card.{id}: 'datasource' is required.");
        }

        SqlReadOnlyValidator.ValidateViewReference(datasource);

        IReadOnlyList<string> bindFilters = [];
        if (props.TryGetValue("bind", out var rawBind) && !string.IsNullOrWhiteSpace(rawBind))
        {
            bindFilters = rawBind
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        return new CardPreset(
            diagram,
            new DataSourceDefinition(DataSourceKind.View, datasource),
            bindFilters);
    }

    private static DiagramPreset ParseDiagramPreset(IReadOnlyDictionary<string, string> props, string id)
    {
        if (!props.TryGetValue("kind", out var kind) || string.IsNullOrWhiteSpace(kind))
        {
            throw new DashSpecParseException($"diagram.{id}: 'kind' is required.");
        }

        try
        {
            DiagramKindRegistry.Resolve(kind);
        }
        catch (ArgumentException ex)
        {
            throw new DashSpecParseException($"diagram.{id}: {ex.Message}");
        }

        props.TryGetValue("render", out var render);
        props.TryGetValue("presentation", out var presentation);
        props.TryGetValue("transform.series", out var seriesTransform);

        var diagramProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in props)
        {
            if (!DiagramPresetReservedKeys.Contains(key))
            {
                diagramProps[key] = value;
            }
        }

        return new DiagramPreset(
            kind,
            string.IsNullOrWhiteSpace(render) ? null : render,
            string.IsNullOrWhiteSpace(presentation) ? null : presentation,
            string.IsNullOrWhiteSpace(seriesTransform) ? null : seriesTransform,
            diagramProps);
    }

    private static SeriesTransformPreset ParseSeriesTransformPreset(
        IReadOnlyDictionary<string, string> props,
        string id)
    {
        if (!props.TryGetValue("max", out var rawMax) ||
            !int.TryParse(rawMax, out var max) ||
            max <= 0)
        {
            throw new DashSpecParseException(
                $"transform.series.{id}: 'max' must be a positive integer.");
        }

        props.TryGetValue("other", out var other);
        return new SeriesTransformPreset(max, string.IsNullOrWhiteSpace(other) ? "Other" : other);
    }
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
