namespace DashSpec.Core.Transforms;

/// <summary>Scalar builtin: IANA zone id → <see cref="UtcOffsetZoneLiteral.Parsed"/> (TimeShift minutes).</summary>
public static class IanaToTimeShiftTransform
{
    public static bool TryNormalizeParameters(
        IReadOnlyDictionary<string, string> raw,
        DateTime referenceUtc,
        out IReadOnlyList<KeyValuePair<string, string>> normalized,
        out string? error)
    {
        normalized = [];
        error = null;

        if (!raw.TryGetValue("iana", out var iana) || string.IsNullOrWhiteSpace(iana))
        {
            if (!raw.TryGetValue("zone", out iana) || string.IsNullOrWhiteSpace(iana))
            {
                error = "iana_to_timeshift requires iana = <IanaZone> (or zone = …).";
                return false;
            }
        }

        if (!IanaTimeZoneConverter.TryGetUtcOffsetMinutes(iana, referenceUtc, out var minutes, out error))
        {
            return false;
        }

        normalized =
        [
            new KeyValuePair<string, string>("iana", iana),
            new KeyValuePair<string, string>("offset_minutes", minutes.ToString()),
        ];
        return true;
    }
}
