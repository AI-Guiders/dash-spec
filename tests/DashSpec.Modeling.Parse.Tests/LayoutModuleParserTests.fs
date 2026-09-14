namespace DashSpec.Modeling.Parse.Tests

open System
open Xunit
open DashSpec.Core.Parsing
open DashSpec.Modeling.Parse.Layout

type LayoutModuleParserTests() =
    [<Fact>]
    member _.``Parse_layout_module_requires_scope`` () =
        let text =
            """
                @layout g
                
                [ Q ]
                """

        let ex = Assert.Throws<DashSpecParseException>(fun () -> LayoutModuleParser.parseLayoutFile text |> ignore)
        Assert.Contains("requires scope", ex.Message, StringComparison.OrdinalIgnoreCase)

    [<Fact>]
    member _.``Parse_toolbar_layout_module_with_board_rows`` () =
        let text =
            """
                @layout tb
                scope toolbar
                
                [ D A ]
                [ U ]
                """

        let board = LayoutModuleParser.parseLayoutFile text

        Assert.Equal(Some LayoutScope.Toolbar, board.ModuleScope)
        Assert.Equal(2, board.RowCount)
        Assert.Equal(2, board.ColumnCount)
        Assert.Equal<string list>([ "D"; "A" ], board.Rows[0] |> Seq.toList)
        Assert.Equal<string list>([ "U" ], board.Rows[1] |> Seq.toList)

    [<Fact>]
    member _.``Parse_tab_layout_module_rejects_invalid_scope`` () =
        let text =
            """
                @layout g
                scope galaxy
                [ Q ]
                """

        let ex = Assert.Throws<DashSpecParseException>(fun () -> LayoutModuleParser.parseLayoutFile text |> ignore)
        Assert.Contains("toolbar, tab, page, or card", ex.Message, StringComparison.OrdinalIgnoreCase)
