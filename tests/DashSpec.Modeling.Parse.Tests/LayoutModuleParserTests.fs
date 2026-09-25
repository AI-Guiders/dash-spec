namespace DashSpec.Modeling.Parse.Tests

open System
open Xunit
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Layout

type LayoutModuleParserTests() =
    [<Fact>]
    member _.``Parse_layout_module_requires_scope`` () =
        let text =
            """
                @layout g
                
                [ Q ]
                """

        let ex = Assert.Throws<DashSpec.Modeling.Core.DashSpecParseException>(fun () -> LayoutModuleParser.parseLayoutFile text |> ignore)
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

        let ex = Assert.Throws<DashSpec.Modeling.Core.DashSpecParseException>(fun () -> LayoutModuleParser.parseLayoutFile text |> ignore)
        Assert.Contains("toolbar, tab, page, card, or host", ex.Message, StringComparison.OrdinalIgnoreCase)

    [<Fact>]
    member _.``Parse_tab_layout_module_with_card_group`` () =
        let text =
            """
                @layout g
                scope tab
                
                group distribution {
                  title = "Distribution"
                  [ A B ]
                }
                [ C ]
                """

        let board = LayoutModuleParser.parseLayoutFile text

        Assert.Equal(2, board.RowCount)
        match board.Entries[0] with
        | GroupRow group ->
            Assert.Equal("distribution", group.Id)
            Assert.Equal(Some "Distribution", group.Title)
            Assert.Equal<string list>([ "A"; "B" ], group.Rows[0] |> Seq.toList)
        | _ -> Assert.Fail("Expected group row")

        match board.Entries[1] with
        | CardRow cells -> Assert.Equal<string list>([ "C" ], cells |> Seq.toList)
        | _ -> Assert.Fail("Expected card row")
