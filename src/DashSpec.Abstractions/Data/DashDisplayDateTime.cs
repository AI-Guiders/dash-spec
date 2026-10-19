namespace DashSpec.Abstractions.Data;

/// <summary>
/// Гражданское (настенное) время из dashflow <c>to_zone</c> вместе с офсетом от UTC (ADR-0079).
/// Слой отображения печатает его как есть — второго сдвига хостовой зоной быть не должно.
/// </summary>
public readonly struct DashDisplayDateTime(DateTime civil, int utcOffsetMinutes = 0) : IEquatable<DashDisplayDateTime>
{
    public DateTime Civil { get; } = DateTime.SpecifyKind(civil, DateTimeKind.Unspecified);

    /// <summary>Офсет гражданского кадра от UTC в минутах (0 — кадр UTC).</summary>
    public int UtcOffsetMinutes { get; } = utcOffsetMinutes;

    public bool Equals(DashDisplayDateTime other) =>
        Civil == other.Civil && UtcOffsetMinutes == other.UtcOffsetMinutes;

    public override bool Equals(object? obj) => obj is DashDisplayDateTime other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Civil, UtcOffsetMinutes);
}
