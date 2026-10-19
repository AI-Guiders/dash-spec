using Microsoft.FSharp.Core;

namespace DashSpec.Core.Tests;

internal static class FilterTestData
{
    public static FilterDefinition Create(
        FilterKind kind,
        string name,
        string? defaultExpression = null,
        string? columnReference = null,
        string? label = null,
        string? widget = null,
        int? minValue = null,
        int? maxValue = null,
        string? grainFilterName = null,
        bool singleSelect = false,
        string? layoutRef = null,
        string? bindScopeHint = null,
        IReadOnlyDictionary<string, string>? grainLabels = null,
        string applyMode = "auto",
        PlacementDefinition? placement = null) =>
        new()
        {
            Kind = kind,
            Name = name,
            DefaultExpression = ToOption(defaultExpression),
            ColumnReference = ToOption(columnReference),
            Label = ToOption(label),
            Widget = ToOption(widget),
            MinValue = ToOption(minValue),
            MaxValue = ToOption(maxValue),
            GrainFilterName = ToOption(grainFilterName),
            SingleSelect = singleSelect,
            LayoutRef = ToOption(layoutRef),
            BindScopeHint = ToOption(bindScopeHint),
            GrainLabels = grainLabels is null
                ? FSharpOption<IReadOnlyDictionary<string, string>>.None
                : FSharpOption<IReadOnlyDictionary<string, string>>.Some(grainLabels),
            ApplyMode = applyMode,
            Placement = placement is null
                ? FSharpOption<PlacementDefinition>.None
                : FSharpOption<PlacementDefinition>.Some(placement),
        };

    private static FSharpOption<string> ToOption(string? value) =>
        value is null ? FSharpOption<string>.None : FSharpOption<string>.Some(value);

    private static FSharpOption<int> ToOption(int? value) =>
        value is int present ? FSharpOption<int>.Some(present) : FSharpOption<int>.None;
}
