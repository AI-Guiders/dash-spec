namespace DashSpec.Modeling.Parse.Layout

open DashSpec.Core.Parsing

/// <summary>
/// M2 pilot entry — delegates lex+parse to canonical C# <see cref="LayoutModuleParser"/>.
/// F# owns IR mapping; no duplicate token layer until Modeling lex spine lands (M4+).
/// </summary>
module LayoutModuleParser =

    let private mapScope (scope: DashSpec.Core.Model.LayoutScope) =
        match scope with
        | DashSpec.Core.Model.LayoutScope.Toolbar -> LayoutScope.Toolbar
        | DashSpec.Core.Model.LayoutScope.Tab -> LayoutScope.Tab
        | DashSpec.Core.Model.LayoutScope.Page -> LayoutScope.Page
        | DashSpec.Core.Model.LayoutScope.Card -> LayoutScope.Card
        | _ -> LayoutScope.Toolbar

    let private fromCore (board: DashSpec.Core.Model.LayoutBoardDefinition) : LayoutBoardDefinition =
        { Rows = board.Rows
          ModuleScope = board.ModuleScope |> Option.ofNullable |> Option.map mapScope }

    /// <summary>Parse a <c>.dashlayout</c> file body via transitional C# parser.</summary>
    let parseLayoutFile (text: string) : LayoutBoardDefinition =
        text
        |> DashSpec.Core.Parsing.LayoutModuleParser.ParseLayoutFile
        |> fromCore
