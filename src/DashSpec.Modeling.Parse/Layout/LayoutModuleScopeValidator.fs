namespace DashSpec.Modeling.Parse.Layout

open System
open DashSpec.Modeling.Core

module LayoutModuleScopeValidator =

    let private formatScope scope =
        match scope with
        | LayoutScope.Toolbar -> "toolbar"
        | LayoutScope.Tab -> "tab"
        | LayoutScope.Page -> "page"
        | LayoutScope.Card -> "card"

    let ensureMatchesIncludeSite (board: LayoutBoardDefinition) (expected: LayoutScope) (context: string) =
        if String.IsNullOrWhiteSpace context then
            invalidArg "context" "Context is required."

        match board.ModuleScope with
        | None -> ()
        | Some scope when scope <> expected ->
            raise (DashSpecParseException($"{context}: layout module declares scope {formatScope scope} but was included for {formatScope expected}."))
        | Some _ -> ()
