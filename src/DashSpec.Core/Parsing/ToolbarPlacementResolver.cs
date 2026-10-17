using DashSpec.Core.Model;
using Microsoft.FSharp.Core;
using FsharpToolbar = DashSpec.Modeling.Parse.Document.ToolbarPlacementResolver;
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
                filters,
                flatNames,
                rows);
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

}
