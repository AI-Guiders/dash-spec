using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal sealed record DiagramKindSpec(
    string Id,
    DiagramDataFamily DataFamily,
    IReadOnlyList<PropertySpec> Properties,
    bool SupportsTopLimit = false,
    bool AllowExtensionProperties = false);

public static class DiagramKindRegistry
{
    private static readonly IReadOnlyList<PropertySpec> ChartProperties =
    [
        new("x", PropertyValueType.ColumnBinding),
        new("y", PropertyValueType.ColumnBinding),
        new("series", PropertyValueType.ColumnBinding),
        new("legend", PropertyValueType.Scalar),
        new("max_series", PropertyValueType.Scalar),
        new("stacked", PropertyValueType.Scalar),
        new("height", PropertyValueType.Scalar),
    ];

    private static readonly IReadOnlyList<PropertySpec> TableProperties =
    [
        new("columns", PropertyValueType.CommaList),
        new("order_by", PropertyValueType.RestOfLine),
        new("limit", PropertyValueType.Scalar),
    ];

    private static readonly IReadOnlyList<PropertySpec> NumberProperties =
    [
        new("value", PropertyValueType.ColumnBinding),
    ];

    private static readonly IReadOnlyList<PropertySpec> HeatmapProperties =
    [
        new("x", PropertyValueType.ColumnBinding),
        new("y", PropertyValueType.ColumnBinding),
        new("value", PropertyValueType.ColumnBinding),
        new("tooltip", PropertyValueType.ColumnBinding),
        new("height", PropertyValueType.Scalar),
    ];

    private static readonly IReadOnlyDictionary<string, DiagramKindSpec> Specs =
        new Dictionary<string, DiagramKindSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["line"] = new("line", DiagramDataFamily.Chart, ChartProperties, AllowExtensionProperties: true),
            ["bar"] = new("bar", DiagramDataFamily.Chart, ChartProperties, AllowExtensionProperties: true),
            ["table"] = new("table", DiagramDataFamily.Table, TableProperties, SupportsTopLimit: true),
            ["number"] = new("number", DiagramDataFamily.Scalar, NumberProperties),
            ["heatmap"] = new("heatmap", DiagramDataFamily.Matrix, HeatmapProperties, AllowExtensionProperties: true),
        };

    public static bool TryResolve(string kind, out DiagramKindInfo info)
    {
        if (Specs.TryGetValue(kind, out var spec))
        {
            info = new DiagramKindInfo(
                spec.Id,
                spec.DataFamily,
                spec.SupportsTopLimit,
                spec.AllowExtensionProperties);
            return true;
        }

        info = default!;
        return false;
    }

    public static DiagramKindInfo Resolve(string kind)
    {
        if (TryResolve(kind, out var info))
        {
            return info;
        }

        var known = string.Join(", ", Specs.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        throw new ArgumentException($"Unknown diagram kind '{kind}'. Known kinds: {known}.");
    }

    public static bool SupportsTopLimit(string kind) =>
        Resolve(kind).SupportsTopLimit;

    internal static IReadOnlyList<PropertySpec> AllBindingProperties()
    {
        var merged = new Dictionary<string, PropertySpec>(StringComparer.OrdinalIgnoreCase);
        foreach (var spec in Specs.Values)
        {
            foreach (var property in spec.Properties)
            {
                merged[property.Name] = property;
            }
        }

        return merged.Values.ToList();
    }

    internal static DiagramKindSpec GetSpec(string kind) => Specs[kind];

    internal static IReadOnlyList<PropertySpec> GetProperties(string kind) =>
        Specs[kind].Properties;
}
