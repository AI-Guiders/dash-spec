#nullable enable

using AIGuiders.Platform.CommandPlane;

namespace DashSpec.Host.Commands.Constructors;

internal static class DateConstructorCatalog
{
    public const string DateTodayId = "date_today";
    public const string DateLeafId = "date";
    public const string WeekGrainLeafId = "week_grain";
    public const string MonthGrainLeafId = "month_grain";
    public const string QuarterGrainLeafId = "quarter_grain";
    public const string DateWeekId = "date_week";
    public const string DateMonthId = "date_month";
    public const string DateQuarterId = "date_quarter";
    public const string DateRangeId = "date_range";

    public static bool IsInstantEntry(string constructorId) =>
        constructorId.Equals(DateTodayId, StringComparison.OrdinalIgnoreCase);

    public static void Register(SlashValueConstructorRegistry registry)
    {
        registry.Register(new SlashLeafConstructorDefinition(
            DateLeafId,
            "Дата",
            [
                new SlashConstructorSegmentDefinition("year", "Год"),
                new SlashConstructorSegmentDefinition("month", "Месяц", WireMinWidth: 2, DisplayMinWidth: 2),
                new SlashConstructorSegmentDefinition("day", "День", WireMinWidth: 2, DisplayMinWidth: 2),
            ],
            WirePattern: "{year}-{month}-{day}",
            DisplayPattern: "{day}.{month}.{year}"));

        registry.Register(new SlashLeafConstructorDefinition(
            WeekGrainLeafId,
            "Неделя",
            [
                new SlashConstructorSegmentDefinition("year", "Год"),
                new SlashConstructorSegmentDefinition("week", "Неделя", WireMinWidth: 2, DisplayMinWidth: 2),
            ],
            WirePattern: "{year}-W{week}",
            DisplayPattern: "W{week} {year}"));

        registry.Register(new SlashLeafConstructorDefinition(
            MonthGrainLeafId,
            "Месяц",
            [
                new SlashConstructorSegmentDefinition("year", "Год"),
                new SlashConstructorSegmentDefinition("month", "Месяц", WireMinWidth: 2, DisplayMinWidth: 2),
            ],
            WirePattern: "{year}-{month}",
            DisplayPattern: "{month}.{year}"));

        registry.Register(new SlashLeafConstructorDefinition(
            QuarterGrainLeafId,
            "Квартал",
            [
                new SlashConstructorSegmentDefinition("year", "Год"),
                new SlashConstructorSegmentDefinition("quarter", "Квартал"),
            ],
            WirePattern: "{year}-{quarter}",
            DisplayPattern: "{quarter} {year}"));

        registry.Register(new SlashCompositeConstructorDefinition(
            DateWeekId,
            "Неделя…",
            [
                new SlashConstructorSlotDefinition("value", WeekGrainLeafId, "Неделя"),
            ],
            WirePattern: "{value}"));

        registry.Register(new SlashCompositeConstructorDefinition(
            DateMonthId,
            "Месяц…",
            [
                new SlashConstructorSlotDefinition("value", MonthGrainLeafId, "Месяц"),
            ],
            WirePattern: "{value}"));

        registry.Register(new SlashCompositeConstructorDefinition(
            DateQuarterId,
            "Квартал…",
            [
                new SlashConstructorSlotDefinition("value", QuarterGrainLeafId, "Квартал"),
            ],
            WirePattern: "{value}"));

        registry.Register(new SlashCompositeConstructorDefinition(
            DateRangeId,
            "Период…",
            [
                new SlashConstructorSlotDefinition("from", DateLeafId, "Дата (с)"),
                new SlashConstructorSlotDefinition("to", DateLeafId, "Дата (по)", SeparatorBefore: ".."),
            ],
            WirePattern: "{from}..{to}"));
    }
}
