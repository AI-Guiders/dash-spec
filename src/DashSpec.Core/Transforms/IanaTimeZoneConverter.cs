namespace DashSpec.Core.Transforms;

/// <summary>IANA tz database ids via BCL <see cref="TimeZoneInfo"/> (ADR-0080).</summary>
public static class IanaTimeZoneConverter
{
    public static bool TryGetUtcOffsetMinutes(string ianaId, DateTime referenceUtc, out int offsetMinutes, out string? error)
    {
        offsetMinutes = 0;
        error = null;

        if (string.IsNullOrWhiteSpace(ianaId))
        {
            error = "IANA zone id is required.";
            return false;
        }

        if (ianaId.Contains(' '))
        {
            error = "IANA zone id cannot contain spaces.";
            return false;
        }

        if (!TryResolveTimeZone(ianaId.Trim(), out var timeZone, out error))
        {
            return false;
        }

        var reference = DateTime.SpecifyKind(referenceUtc, DateTimeKind.Utc);
        offsetMinutes = (int)timeZone.GetUtcOffset(reference).TotalMinutes;
        return true;
    }

    private static bool TryResolveTimeZone(string timeZoneId, out TimeZoneInfo timeZone, out string? error)
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var found))
        {
            timeZone = found;
            error = null;
            return true;
        }

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out var windowsId)
            && TimeZoneInfo.TryFindSystemTimeZoneById(windowsId, out found))
        {
            timeZone = found;
            error = null;
            return true;
        }

        timeZone = null!;
        error = $"Unknown IANA time zone '{timeZoneId}'.";
        return false;
    }
}
