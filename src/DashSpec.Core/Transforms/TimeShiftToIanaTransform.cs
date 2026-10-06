namespace DashSpec.Core.Transforms;

/// <summary>
/// Scalar builtin: TimeShift (minutes) → IANA id at the reference UTC instant passed to TryNormalizeParameters.
/// Lossy: many zones share the same offset; picks a stable representative (prefers <c>Area/City</c> ids).
/// </summary>
public static class TimeShiftToIanaTransform
{
    public static bool TryNormalizeParameters(
        IReadOnlyDictionary<string, string> raw,
        DateTime referenceUtc,
        out IReadOnlyList<KeyValuePair<string, string>> normalized,
        out string? error)
    {
        normalized = [];
        error = null;

        if (!TryReadOffsetMinutes(raw, out var minutes, out error))
        {
            return false;
        }

        if (!TryResolveIana(minutes, referenceUtc, out var iana, out error))
        {
            return false;
        }

        normalized =
        [
            new KeyValuePair<string, string>("offset_minutes", minutes.ToString()),
            new KeyValuePair<string, string>("iana", iana),
            new KeyValuePair<string, string>("lossy", "true"),
        ];
        return true;
    }

    private static bool TryReadOffsetMinutes(IReadOnlyDictionary<string, string> raw, out int minutes, out string? error)
    {
        minutes = 0;
        error = null;

        if (raw.TryGetValue("offset_minutes", out var rawMinutes)
            && int.TryParse(rawMinutes, out minutes))
        {
            return UtcOffsetZoneLiteral.TryParseOffsetMinutes(minutes, out _, out error);
        }

        if (raw.TryGetValue("zone", out var zone)
            && UtcOffsetZoneLiteral.TryParse(zone, out var parsed, out error))
        {
            minutes = parsed.TotalMinutes;
            return true;
        }

        error = "timeshift_to_iana requires zone = UTC±… or offset_minutes = <integer>.";
        return false;
    }

    private static bool TryResolveIana(int offsetMinutes, DateTime referenceUtc, out string iana, out string? error)
    {
        iana = string.Empty;
        error = null;
        var reference = DateTime.SpecifyKind(referenceUtc, DateTimeKind.Utc);

        string? best = null;

        foreach (var timeZone in TimeZoneInfo.GetSystemTimeZones())
        {
            if ((int)timeZone.GetUtcOffset(reference).TotalMinutes != offsetMinutes)
            {
                continue;
            }

            var candidate = timeZone.Id;
            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(candidate, out var ianaId))
            {
                candidate = ianaId;
            }

            if (candidate.Contains('/') && !candidate.StartsWith("Etc/", StringComparison.OrdinalIgnoreCase))
            {
                best = PickBetter(best, candidate);
            }
            else
            {
                best = PickBetter(best, candidate);
            }
        }

        if (string.IsNullOrEmpty(best))
        {
            error = $"No IANA zone found for offset {offsetMinutes} minutes at the reference instant.";
            return false;
        }

        iana = best;
        return true;
    }

    private static string PickBetter(string? current, string candidate) =>
        current is null || string.Compare(candidate, current, StringComparison.OrdinalIgnoreCase) < 0
            ? candidate
            : current;
}
