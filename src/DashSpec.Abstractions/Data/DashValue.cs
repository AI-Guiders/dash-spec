namespace DashSpec.Abstractions.Data;

/// <summary>One primitive cell on the typed row wire (ADR-0087).</summary>
public readonly struct DashValue : IEquatable<DashValue>
{
    private readonly bool _isNull;
    private readonly bool _bool;
    private readonly int _int;
    private readonly decimal _decimal;
    private readonly string? _string;
    private readonly long _durationTicks;
    private readonly DateOnly _date;
    private readonly TimeOnly _time;
    private readonly DateTime _dateTimeUtc;

    private DashValue(DashPrimitiveKind kind, bool isNull)
    {
        Kind = kind;
        _isNull = isNull;
    }

    public static DashValue NullOf(DashPrimitiveKind kind) => new(kind, isNull: true);

    private DashValue(bool value)
    {
        Kind = DashPrimitiveKind.Bool;
        _bool = value;
    }

    private DashValue(int value)
    {
        Kind = DashPrimitiveKind.Int;
        _int = value;
    }

    private DashValue(decimal value)
    {
        Kind = DashPrimitiveKind.Decimal;
        _decimal = value;
    }

    private DashValue(string value)
    {
        Kind = DashPrimitiveKind.String;
        _string = value;
    }

    private DashValue(TimeSpan duration)
    {
        Kind = DashPrimitiveKind.Duration;
        _durationTicks = duration.Ticks;
    }

    private DashValue(DateOnly date)
    {
        Kind = DashPrimitiveKind.Date;
        _date = date;
    }

    private DashValue(TimeOnly time)
    {
        Kind = DashPrimitiveKind.Time;
        _time = time;
    }

    private DashValue(DateTime dateTimeUtc)
    {
        Kind = DashPrimitiveKind.DateTime;
        _dateTimeUtc = DateTime.SpecifyKind(dateTimeUtc, DateTimeKind.Utc);
    }

    public DashPrimitiveKind Kind { get; }

    public bool IsNull => _isNull;

    public static DashValue FromBool(bool value) => new(value);

    public static DashValue FromInt(int value) => new(value);

    public static DashValue FromDecimal(decimal value) => new(value);

    public static DashValue FromString(string value) => new(value);

    public static DashValue FromDuration(TimeSpan value) => new(value);

    public static DashValue FromDate(DateOnly value) => new(value);

    public static DashValue FromTime(TimeOnly value) => new(value);

    public static DashValue FromDateTimeUtc(DateTime value) => new(value);

    public bool AsBool() => Kind == DashPrimitiveKind.Bool && !_isNull
        ? _bool
        : throw new InvalidOperationException($"Expected bool, got {Kind} null={_isNull}.");

    public int AsInt32() => Kind == DashPrimitiveKind.Int && !_isNull
        ? _int
        : throw new InvalidOperationException($"Expected int, got {Kind} null={_isNull}.");

    public decimal AsDecimal() => Kind == DashPrimitiveKind.Decimal && !_isNull
        ? _decimal
        : throw new InvalidOperationException($"Expected decimal, got {Kind} null={_isNull}.");

    public string AsString() => Kind == DashPrimitiveKind.String && !_isNull
        ? _string ?? string.Empty
        : throw new InvalidOperationException($"Expected string, got {Kind} null={_isNull}.");

    public TimeSpan AsDuration() => Kind == DashPrimitiveKind.Duration && !_isNull
        ? TimeSpan.FromTicks(_durationTicks)
        : throw new InvalidOperationException($"Expected duration, got {Kind} null={_isNull}.");

    public DateOnly AsDate() => Kind == DashPrimitiveKind.Date && !_isNull
        ? _date
        : throw new InvalidOperationException($"Expected date, got {Kind} null={_isNull}.");

    public TimeOnly AsTime() => Kind == DashPrimitiveKind.Time && !_isNull
        ? _time
        : throw new InvalidOperationException($"Expected time, got {Kind} null={_isNull}.");

    public DateTime AsDateTimeUtc() => Kind == DashPrimitiveKind.DateTime && !_isNull
        ? _dateTimeUtc
        : throw new InvalidOperationException($"Expected datetime, got {Kind} null={_isNull}.");

    /// <summary>CLR projection for present-layer formatters (LabelFormat).</summary>
    public object? ToClr() =>
        _isNull
            ? null
            : Kind switch
            {
                DashPrimitiveKind.Bool => _bool,
                DashPrimitiveKind.Int => _int,
                DashPrimitiveKind.Decimal => _decimal,
                DashPrimitiveKind.String => _string,
                DashPrimitiveKind.Duration => TimeSpan.FromTicks(_durationTicks),
                DashPrimitiveKind.Date => _date,
                DashPrimitiveKind.Time => _time,
                DashPrimitiveKind.DateTime => _dateTimeUtc,
                _ => null,
            };

    public bool Equals(DashValue other) =>
        Kind == other.Kind
        && _isNull == other._isNull
        && Kind switch
        {
            DashPrimitiveKind.Bool => _bool == other._bool,
            DashPrimitiveKind.Int => _int == other._int,
            DashPrimitiveKind.Decimal => _decimal == other._decimal,
            DashPrimitiveKind.String => string.Equals(_string, other._string, StringComparison.Ordinal),
            DashPrimitiveKind.Duration => _durationTicks == other._durationTicks,
            DashPrimitiveKind.Date => _date == other._date,
            DashPrimitiveKind.Time => _time == other._time,
            DashPrimitiveKind.DateTime => _dateTimeUtc == other._dateTimeUtc,
            _ => true,
        };

    public override bool Equals(object? obj) => obj is DashValue other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Kind, ToClr());
}
