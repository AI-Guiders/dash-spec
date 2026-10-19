using Microsoft.FSharp.Core;

namespace DashSpec.Platform.Session.Tests;

internal static class FilterTestData
{
    public static FilterDefinition Date(string name, string? defaultExpression, string? columnReference) =>
        new()
        {
            Kind = FilterKind.Date,
            Name = name,
            DefaultExpression = ToOption(defaultExpression),
            ColumnReference = ToOption(columnReference),
            Label = FSharpOption<string>.None,
            Widget = FSharpOption<string>.None,
            MinValue = FSharpOption<int>.None,
            MaxValue = FSharpOption<int>.None,
            GrainFilterName = FSharpOption<string>.None,
            SingleSelect = false,
            LayoutRef = FSharpOption<string>.None,
            BindScopeHint = FSharpOption<string>.None,
            GrainLabels = FSharpOption<IReadOnlyDictionary<string, string>>.None,
            ApplyMode = "auto",
            Placement = FSharpOption<PlacementDefinition>.None,
        };

    private static FSharpOption<string> ToOption(string? value) =>
        value is null ? FSharpOption<string>.None : FSharpOption<string>.Some(value);
}
