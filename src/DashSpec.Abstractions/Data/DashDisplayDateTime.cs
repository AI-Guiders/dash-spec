namespace DashSpec.Abstractions.Data;

/// <summary>
/// Civil wall-clock instant from dashflow <c>to_zone</c> (ADR-0078).
/// Present layer formats this as-is (no second host/report time-zone shift).
/// </summary>
public readonly struct DashDisplayDateTime(DateTime civil) : IEquatable<DashDisplayDateTime>
{
    public DateTime Civil { get; } = DateTime.SpecifyKind(civil, DateTimeKind.Unspecified);

    public bool Equals(DashDisplayDateTime other) => Civil == other.Civil;

    public override bool Equals(object? obj) => obj is DashDisplayDateTime other && Equals(other);

    public override int GetHashCode() => Civil.GetHashCode();
}
