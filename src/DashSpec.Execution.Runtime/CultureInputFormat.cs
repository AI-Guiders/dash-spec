using System.Globalization;
using System.Text;

namespace DashSpec.Execution.Runtime;

/// <summary>
/// Maps a report culture to flatpickr input options (display tokens, locale,
/// week start). Display format follows the spec culture, never the browser locale.
/// Date parsing/round-trips are not here: they live in DateValueCodec / BCL parsers.
/// </summary>
public static class CultureInputFormat
{
    /// <summary>flatpickr day tokens for the culture's short date pattern (ru-RU: dd.MM.yyyy → d.m.Y).</summary>
    public static string DayPattern(CultureInfo culture) =>
        ToFlatpickrPattern(culture.DateTimeFormat.ShortDatePattern);

    /// <summary>flatpickr time tokens for the culture's short time pattern (ru-RU: HH:mm → H:i).</summary>
    public static string TimePattern(CultureInfo culture) =>
        ToFlatpickrPattern(culture.DateTimeFormat.ShortTimePattern);

    /// <summary>flatpickr month-select alt pattern (ru-RU: MMMM yyyy → F Y).</summary>
    public static string MonthPattern(CultureInfo culture) =>
        ToFlatpickrPattern(culture.DateTimeFormat.YearMonthPattern);

    /// <summary>flatpickr l10n key (two-letter ISO; English is flatpickr's built-in default).</summary>
    public static string Locale(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName.ToLowerInvariant();

    /// <summary>flatpickr weekStart (0 = Sunday) from the culture's first day of week.</summary>
    public static int WeekStart(CultureInfo culture) =>
        (int)culture.DateTimeFormat.FirstDayOfWeek;

    /// <summary>Translates a .NET date/time pattern into flatpickr tokens (format vocabularies differ).</summary>
    public static string ToFlatpickrPattern(string dotnetPattern)
    {
        if (string.IsNullOrWhiteSpace(dotnetPattern))
        {
            return "Y-m-d";
        }

        var result = new StringBuilder(dotnetPattern.Length + 4);
        for (var i = 0; i < dotnetPattern.Length; i++)
        {
            var run = Run(dotnetPattern, i);
            switch (dotnetPattern[i])
            {
                case 'y':
                    result.Append(run >= 4 ? 'Y' : 'y');
                    break;
                case 'M':
                    result.Append(run >= 4 ? 'F' : run == 3 ? 'M' : run == 2 ? 'm' : 'n');
                    break;
                case 'd':
                    result.Append(run >= 2 ? 'd' : 'j');
                    break;
                case 'H':
                    result.Append(run >= 2 ? 'H' : 'G');
                    break;
                case 'h':
                    result.Append(run >= 2 ? 'h' : 'g');
                    break;
                case 'm':
                    result.Append('i');
                    break;
                case 's':
                    result.Append('S');
                    break;
                case 't':
                    result.Append('K');
                    break;
                default:
                    result.Append(dotnetPattern[i]);
                    break;
            }

            i += run - 1;
        }

        return result.ToString();
    }

    private static int Run(string pattern, int start)
    {
        var length = 1;
        while (start + length < pattern.Length && pattern[start + length] == pattern[start])
        {
            length++;
        }

        return length;
    }
}
