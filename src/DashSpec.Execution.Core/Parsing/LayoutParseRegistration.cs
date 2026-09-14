using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using Microsoft.FSharp.Core;
using FsharpBoard = DashSpec.Modeling.Parse.Layout.LayoutBoardDefinition;
using FsharpScope = DashSpec.Modeling.Parse.Layout.LayoutScope;

namespace DashSpec.Execution.Parsing;

/// <summary>Wire F# layout SSOT into Core parse bridge (ADR-0048 M2/M3).</summary>
internal static class LayoutParseRegistration
{
    static LayoutParseRegistration()
    {
        LayoutParseBridge.ParseLayoutFile = text =>
        {
            try
            {
                return ToCore(DashSpec.Modeling.Parse.Layout.LayoutModuleParser.parseLayoutFile(text));
            }
            catch (DashSpec.Modeling.Core.DashSpecParseException ex)
            {
                throw new DashSpecParseException(ex.Message, ex.SourceOffset);
            }
        };
    }

    internal static void EnsureRegistered() => _ = typeof(LayoutParseRegistration);

    private static LayoutScope? MapScope(FsharpScope scope)
    {
        if (scope.Equals(FsharpScope.Toolbar)) return LayoutScope.Toolbar;
        if (scope.Equals(FsharpScope.Tab)) return LayoutScope.Tab;
        if (scope.Equals(FsharpScope.Page)) return LayoutScope.Page;
        if (scope.Equals(FsharpScope.Card)) return LayoutScope.Card;
        return null;
    }

    private static LayoutBoardDefinition ToCore(FsharpBoard board)
    {
        var scopes = OptionModule.ToArray(board.ModuleScope);
        LayoutScope? moduleScope = scopes.Length > 0 ? MapScope(scopes[0]) : null;
        return new LayoutBoardDefinition(board.Rows, moduleScope);
    }
}
