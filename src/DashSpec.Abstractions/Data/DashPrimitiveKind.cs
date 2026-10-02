namespace DashSpec.Abstractions.Data;

/// <summary>Closed primitive set at the row wire boundary (ADR-0079 §3).</summary>
public enum DashPrimitiveKind
{
    Bool = 1,
    Int = 2,
    Decimal = 3,
    String = 4,
    Duration = 5,
    Date = 6,
    Time = 7,
    DateTime = 8,
}
