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

/// <summary>Bracket layout board; optional <see cref="ModuleScope"/> when loaded from <c>.dashlayout</c>.</summary>
[<CLIMutable>]
type LayoutBoardDefinition =
    { Rows: IReadOnlyList<IReadOnlyList<string>>
      ModuleScope: LayoutScope option }

    member this.RowCount = this.Rows.Count

    member this.ColumnCount =
        if this.Rows.Count = 0 then 0
        else this.Rows |> Seq.map (fun row -> row.Count) |> Seq.max
