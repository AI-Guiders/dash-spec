namespace DashSpec.Core.Model;

/// <summary>Card visibility precondition (ADR-0030; keyword <c>when</c>, not gate).</summary>
public sealed record CardVisibilityRule(
    string FilterName,
    CardVisibilityMode Mode,
    string? Message = null);

public enum CardVisibilityMode
{
    /// <summary>Show card only when the bound filter has no selection.</summary>
    WhenEmpty,

    /// <summary>Show card only when the bound filter has a selection.</summary>
    WhenSet,
}
