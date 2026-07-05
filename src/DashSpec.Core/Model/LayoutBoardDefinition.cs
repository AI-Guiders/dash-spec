namespace DashSpec.Core.Model;

/// <summary>Bracket layout board; optional <see cref="ModuleScope"/> when loaded from <c>.dashlayout</c>.</summary>
public sealed record LayoutBoardDefinition(
    IReadOnlyList<IReadOnlyList<string>> Rows,
    LayoutScope? ModuleScope = null)
{
    public int RowCount => Rows.Count;

    public int ColumnCount => Rows.Count == 0 ? 0 : Rows.Max(static row => row.Count);
}
