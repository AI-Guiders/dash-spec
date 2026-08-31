#nullable enable

using System.Globalization;
using AIGuiders.Platform.CommandPlane;

namespace DashSpec.Host.Commands.Constructors;

public sealed class DateConstructorSegmentProvider : IConstructorSegmentProvider
{
    readonly IDashboardCultureAmbient _cultureAmbient;

    public DateConstructorSegmentProvider(IDashboardCultureAmbient cultureAmbient) =>
        _cultureAmbient = cultureAmbient;

    public DateOnly Today { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    CultureInfo ActiveCulture => _cultureAmbient.Culture;

    public IReadOnlyList<ArgCompletionItem> GetSegmentSuggestions(
        LeafConstructorDefinition leaf,
        int segmentIndex,
        ArgConstructorDraft draft,
        string partial)
    {
        if (segmentIndex < 0 || segmentIndex >= leaf.Segments.Count)
        {
            return [];
        }

        var segment = leaf.Segments[segmentIndex];
        return segment.SegmentId.ToLowerInvariant() switch
        {
            "year" => BuildYearSuggestions(partial),
            "month" => BuildMonthSuggestions(draft, partial),
            "day" => BuildDaySuggestions(draft, partial),
            "week" => BuildWeekSuggestions(draft, partial),
            "month_week" => BuildMonthWeekSuggestions(draft, partial),
            "quarter" => BuildQuarterSuggestions(partial),
            _ => [],
        };
    }

    IReadOnlyList<ArgCompletionItem> BuildYearSuggestions(string partial)
    {
        var first = Today.Year - 10;
        var last = Today.Year + 2;
        var items = new List<ArgCompletionItem>();
        for (var year = last; year >= first; year--)
        {
            var text = year.ToString(CultureInfo.InvariantCulture);
            if (!MatchesPartial(text, partial))
            {
                continue;
            }

            items.Add(new ArgCompletionItem(
                text,
                "",
                text,
                "Date",
                text,
                ArgCompletionItemKind.ConstructorStep,
                text));
        }

        return items;
    }

    IReadOnlyList<ArgCompletionItem> BuildMonthSuggestions(ArgConstructorDraft draft, string partial)
    {
        if (!TryReadYear(draft, out var year))
        {
            return [];
        }

        var items = new List<ArgCompletionItem>();
        for (var month = 1; month <= 12; month++)
        {
            var wire = month.ToString("00", CultureInfo.InvariantCulture);
            var label = ActiveCulture.DateTimeFormat.GetMonthName(month);
            if (!MatchesPartial(wire, partial) && !MatchesPartial(label, partial))
            {
                continue;
            }

            items.Add(new ArgCompletionItem(
                wire,
                "",
                label,
                "Date",
                label,
                ArgCompletionItemKind.ConstructorStep,
                wire));
        }

        return items;
    }

    IReadOnlyList<ArgCompletionItem> BuildDaySuggestions(ArgConstructorDraft draft, string partial)
    {
        if (!TryReadYear(draft, out var year) || !TryReadMonth(draft, out var month))
        {
            return [];
        }

        var days = DateTime.DaysInMonth(year, month);
        var items = new List<ArgCompletionItem>();
        for (var day = 1; day <= days; day++)
        {
            var wire = day.ToString("00", CultureInfo.InvariantCulture);
            if (!MatchesPartial(wire, partial))
            {
                continue;
            }

            items.Add(new ArgCompletionItem(
                wire,
                "",
                wire,
                "Date",
                wire,
                ArgCompletionItemKind.ConstructorStep,
                wire));
        }

        return items;
    }

    IReadOnlyList<ArgCompletionItem> BuildWeekSuggestions(ArgConstructorDraft draft, string partial)
    {
        if (!TryReadYear(draft, out var year))
        {
            return [];
        }

        var weeksInYear = ISOWeek.GetWeeksInYear(year);
        var items = new List<ArgCompletionItem>();
        for (var week = weeksInYear; week >= 1; week--)
        {
            var wire = week.ToString("00", CultureInfo.InvariantCulture);
            var label = $"Неделя {week}";
            if (!MatchesPartial(wire, partial) && !MatchesPartial(label, partial))
            {
                continue;
            }

            items.Add(new ArgCompletionItem(
                wire,
                "",
                label,
                "Date",
                label,
                ArgCompletionItemKind.ConstructorStep,
                wire));
        }

        return items;
    }

    IReadOnlyList<ArgCompletionItem> BuildMonthWeekSuggestions(ArgConstructorDraft draft, string partial)
    {
        if (!TryReadYear(draft, out var year) || !TryReadMonth(draft, out var month))
        {
            return [];
        }

        var daysInMonth = DateTime.DaysInMonth(year, month);
        var maxWeek = (daysInMonth + 6) / 7;
        var items = new List<ArgCompletionItem>();
        for (var week = 1; week <= maxWeek; week++)
        {
            var wire = week.ToString(CultureInfo.InvariantCulture);
            var label = $"{week}-я неделя месяца";
            if (!MatchesPartial(wire, partial) && !MatchesPartial(label, partial))
            {
                continue;
            }

            items.Add(new ArgCompletionItem(
                wire,
                "",
                label,
                "Date",
                label,
                ArgCompletionItemKind.ConstructorStep,
                wire));
        }

        return items;
    }

    IReadOnlyList<ArgCompletionItem> BuildQuarterSuggestions(string partial)
    {
        var items = new List<ArgCompletionItem>();
        for (var quarter = 1; quarter <= 4; quarter++)
        {
            var wire = $"Q{quarter}";
            var label = quarter switch
            {
                1 => "I квартал (Q1)",
                2 => "II квартал (Q2)",
                3 => "III квартал (Q3)",
                _ => "IV квартал (Q4)",
            };
            if (!MatchesPartial(wire, partial) && !MatchesPartial(label, partial))
            {
                continue;
            }

            items.Add(new ArgCompletionItem(
                wire,
                "",
                label,
                "Date",
                label,
                ArgCompletionItemKind.ConstructorStep,
                wire));
        }

        return items;
    }

    static bool TryReadYear(ArgConstructorDraft draft, out int year) =>
        int.TryParse(ReadSegment(draft, "year"), out year);

    static bool TryReadMonth(ArgConstructorDraft draft, out int month) =>
        int.TryParse(ReadSegment(draft, "month"), out month);

    static string? ReadSegment(ArgConstructorDraft draft, string segmentId) =>
        draft.ActiveSegments.TryGetValue(segmentId, out var value) ? value : null;

    static bool MatchesPartial(string value, string partial) =>
        partial.Length == 0 || value.StartsWith(partial, StringComparison.OrdinalIgnoreCase);
}
