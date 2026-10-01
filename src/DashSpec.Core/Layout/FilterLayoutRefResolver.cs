using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using Microsoft.FSharp.Core;
using FsharpFilter = DashSpec.Modeling.Parse.Filter.FilterDefinition;
using FsharpFilterKind = DashSpec.Modeling.Parse.Filter.FilterKind;

namespace DashSpec.Core.Layout;

/// <summary>SSOT: <c>DashSpec.Modeling.Parse.Filter.FilterLayoutRefResolver</c> (F#).</summary>
internal static class FilterLayoutRefResolver
{
    public static string Resolve(string token, IReadOnlyList<FilterDefinition> filters, string context)
    {
        try
        {
            return Modeling.Parse.Filter.FilterLayoutRefResolver.resolve(
                token,
                filters.Select(ToFsharp).ToList(),
                context);
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    private static FsharpFilter ToFsharp(FilterDefinition filter) =>
        new()
        {
            Kind = MapKind(filter.Kind),
            Name = filter.Name,
            DefaultExpression = ToFsharpStringOption(filter.DefaultExpression),
            ColumnReference = ToFsharpStringOption(filter.ColumnReference),
            Label = ToFsharpStringOption(filter.Label),
            Widget = ToFsharpStringOption(filter.Widget),
            MinValue = ToFsharpIntOption(filter.MinValue),
            MaxValue = ToFsharpIntOption(filter.MaxValue),
            GrainFilterName = ToFsharpStringOption(filter.GrainFilterName),
            SingleSelect = filter.SingleSelect,
            LayoutRef = ToFsharpStringOption(filter.LayoutRef),
            BindScopeHint = ToFsharpStringOption(filter.BindScopeHint),
            GrainLabels = FSharpOption<IReadOnlyDictionary<string, string>>.None,
            Placement = FSharpOption<Modeling.Parse.Layout.PlacementDefinition>.None,
        };

    private static FsharpFilterKind MapKind(FilterKind kind) =>
        kind switch
        {
            FilterKind.Date => FsharpFilterKind.Date,
            FilterKind.Field => FsharpFilterKind.Field,
            FilterKind.Top => FsharpFilterKind.Top,
            _ => FsharpFilterKind.Field,
        };

    private static FSharpOption<string> ToFsharpStringOption(string? value) =>
        string.IsNullOrWhiteSpace(value) ? FSharpOption<string>.None : FSharpOption<string>.Some(value);

    private static FSharpOption<int> ToFsharpIntOption(int? value) =>
        value is int n ? FSharpOption<int>.Some(n) : FSharpOption<int>.None;
}
