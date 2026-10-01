using DashSpec.Core.Model;
using Microsoft.FSharp.Core;
using FsharpToolbar = DashSpec.Modeling.Parse.Document.ToolbarPlacementResolver;
using FsharpFilter = DashSpec.Modeling.Parse.Filter.FilterDefinition;
using FsharpFilterKind = DashSpec.Modeling.Parse.Filter.FilterKind;
namespace DashSpec.Core.Parsing;

/// <summary>SSOT: <c>DashSpec.Modeling.Parse.Document.ToolbarPlacementResolver</c> (F#).</summary>
internal static class ToolbarPlacementResolver
{
    public static IReadOnlyList<string> ResolveFilterNames(
        IReadOnlyList<FilterDefinition> filters,
        IReadOnlyList<string> flatNames,
        LayoutBoardDefinition? board)
    {
        try
        {
            FSharpOption<IReadOnlyList<IReadOnlyList<string>>> rows =
                board is null
                    ? FSharpOption<IReadOnlyList<IReadOnlyList<string>>>.None
                    : FSharpOption<IReadOnlyList<IReadOnlyList<string>>>.Some(board.Rows);

            return FsharpToolbar.resolveFilterNamesFromRows(
                filters.Select(ToFsharpFilter).ToList(),
                flatNames,
                rows);
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    private static FsharpFilter ToFsharpFilter(FilterDefinition filter) =>
        new()
        {
            Kind = filter.Kind switch
            {
                FilterKind.Date => FsharpFilterKind.Date,
                FilterKind.Field => FsharpFilterKind.Field,
                FilterKind.Top => FsharpFilterKind.Top,
                _ => FsharpFilterKind.Field,
            },
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

    private static FSharpOption<string> ToFsharpStringOption(string? value) =>
        string.IsNullOrWhiteSpace(value) ? FSharpOption<string>.None : FSharpOption<string>.Some(value);

    private static FSharpOption<int> ToFsharpIntOption(int? value) =>
        value is int n ? FSharpOption<int>.Some(n) : FSharpOption<int>.None;
}
