namespace DashSpec.Core.Model;

/// <summary>Tab-level bracket layout: each row is a list of card ref or id tokens.</summary>
public sealed record LayoutBoardDefinition(IReadOnlyList<IReadOnlyList<string>> Rows)
{
    public int RowCount => Rows.Count;

    public int ColumnCount => Rows.Count == 0 ? 0 : Rows.Max(static row => row.Count);
}
