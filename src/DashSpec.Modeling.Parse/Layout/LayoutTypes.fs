namespace DashSpec.Modeling.Parse.Layout

open System.Collections.Generic

/// <summary>Declared scope of a <c>.dashlayout</c> module (ADR-0026).</summary>
type LayoutScope =
    | Toolbar
    | Tab
    | Page
    | Card
    | Host

/// <summary>Grid placement for a card or filter (row/col/span).</summary>
[<CLIMutable>]
type PlacementDefinition =
    { Row: int
      Col: int
      Span: int }

[<CLIMutable>]
type LayoutDefinition =
    { Columns: int
      GapPx: int }

    static member Default = { Columns = 12; GapPx = 16 }

/// <summary>GroupBox-style card group inside a tab/page layout board (ADR-0056).</summary>
[<CLIMutable>]
type LayoutBoardGroupDefinition =
    { Id: string
      Title: string option
      Rows: IReadOnlyList<IReadOnlyList<string>> }

/// <summary>Top-level layout board entry.</summary>
type LayoutBoardEntry =
    | CardRow of IReadOnlyList<string>
    | GroupRow of LayoutBoardGroupDefinition

/// <summary>Bracket layout board; optional <see cref="ModuleScope"/> when loaded from <c>.dashlayout</c>.</summary>
[<CLIMutable>]
type LayoutBoardDefinition =
    { Entries: IReadOnlyList<LayoutBoardEntry>
      ModuleScope: LayoutScope option }

    /// <summary>Top-level card rows only (toolbar/host compat).</summary>
    member this.Rows =
        this.Entries
        |> Seq.choose (function CardRow cells -> Some cells | _ -> None)
        |> Seq.toList

    member this.RowCount = this.Entries.Count

    member this.ColumnCount =
        let counts =
            this.Entries
            |> Seq.collect (fun entry ->
                match entry with
                | CardRow cells -> seq { cells.Count }
                | GroupRow group -> group.Rows |> Seq.map (fun row -> row.Count))
        if Seq.isEmpty counts then 0 else Seq.max counts
