using System.Text.RegularExpressions;

namespace DashSpec.Core;

/// <summary>Fixed UTC offset literals for dashflow <c>to_zone</c> (ADR-0078/0080). IANA ids are not accepted.</summary>
public static partial class UtcOffsetZoneLiteral
{
    [GeneratedRegex(
        @"^UTC(?<sign>[+-])?(?:(?<hours>\d{1,2})(?::(?<minutes>\d{2}))?)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ZonePattern();

    public readonly record struct Parsed(int TotalMinutes);

    public static bool TryParse(string? text, out Parsed parsed, out string? error)
    {
        parsed = default;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "zone value is required.";
            return false;
        }

        var trimmed = text.Trim();

        if (trimmed.Contains('/') || trimmed.Contains('\\'))
        {
            error = "zone must be a fixed UTC offset (e.g. UTC+3), not an IANA or Windows time zone id.";
            return false;
        }

        if (trimmed.Contains(' '))
        {
            error = "zone must be a fixed UTC offset (e.g. UTC+3), not a named time zone.";
            return false;
        }

        var match = ZonePattern().Match(trimmed);
        if (!match.Success)
        {
            error = "zone must match UTC, UTC+3, UTC+03:30, or use offset_minutes = <integer>.";
            return false;
        }

        var sign = match.Groups["sign"].Value == "-" ? -1 : 1;
        var hoursGroup = match.Groups["hours"];

        if (!hoursGroup.Success)
        {
            parsed = new Parsed(0);
            return true;
        }

        var hours = int.Parse(hoursGroup.Value);
        var minutes = match.Groups["minutes"].Success ? int.Parse(match.Groups["minutes"].Value) : 0;

        if (hours > 14 || minutes > 59)
        {
            error = "UTC offset hours must be 0–14 and minutes 0–59.";
            return false;
        }

        parsed = new Parsed(sign * (hours * 60 + minutes));
        return true;
    }

    public static bool TryParseOffsetMinutes(int minutes, out Parsed parsed, out string? error)
    {
        parsed = default;
        error = null;

        if (minutes < -14 * 60 || minutes > 14 * 60)
        {
            error = "offset_minutes must be between -840 and 840 (±14:00).";
            return false;
        }

        parsed = new Parsed(minutes);
        return true;
    }
}
