namespace DashSpec.Core.Model;

/// <summary>Dashboard grid (12-col default). Not PlantUML — explicit grid placement for cards.</summary>
public sealed record LayoutDefinition(int Columns = 12, int GapPx = 16)
{
    public static LayoutDefinition Default { get; } = new();
}

/// <summary>Card position on the dashboard grid (1-based row/col).</summary>
public sealed record PlacementDefinition(int Row = 1, int Col = 1, int Span = 6);
