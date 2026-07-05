namespace DashSpec.Core.Model;

/// <summary>Bounded click interaction on a card (ADR-0028).</summary>
public sealed record CardClickBehaviour(IReadOnlyList<CardClickEffect> Effects);

public abstract record CardClickEffect;

public sealed record ShowSelectionEffect(
    ShowPlacement Placement,
    ShowFormat Format,
    ShowSource Source,
    bool CopyFriendly) : CardClickEffect;

public sealed record SetFilterFromFieldEffect(
    string FilterName,
    string Field) : CardClickEffect;

public sealed record InvokeHandlerEffect(
    string HandlerId,
    IReadOnlyDictionary<string, string> Args) : CardClickEffect;

public sealed record GotoTabEffect(string TabId) : CardClickEffect;

public enum ShowPlacement
{
    Below,
}

public enum ShowFormat
{
    List,
    Plain,
    Kv,
}

public enum ShowSource
{
    Tooltip,
    Cell,
}

/// <summary>Selected heatmap cell context for inspect/act handlers.</summary>
public sealed record HeatmapCellContext(
    int Yi,
    int Xi,
    string XLabel,
    string YLabel,
    double? Value,
    string? TooltipRaw);
