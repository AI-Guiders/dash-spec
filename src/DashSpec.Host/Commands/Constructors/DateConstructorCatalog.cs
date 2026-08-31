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
    public const string MonthWeekGrainLeafId = "month_week_grain";
    public const string DateWeekId = "date_week";
    public const string DateMonthWeekId = "date_month_week";
    public const string DateMonthId = "date_month";
    public const string DateQuarterId = "date_quarter";
    public const string DateRangeId = "date_range";

    public static bool IsInstantEntry(string constructorId) =>
        constructorId.Equals(DateTodayId, StringComparison.OrdinalIgnoreCase);

    public static void Register(ValueConstructorRegistry registry)
    {
        registry.Register(new LeafConstructorDefinition(
            DateLeafId,
            "Дата",
            [
                new ConstructorSegmentDefinition("year", "Год"),
                new ConstructorSegmentDefinition("month", "Месяц", WireMinWidth: 2, DisplayMinWidth: 2),
                new ConstructorSegmentDefinition("day", "День", WireMinWidth: 2, DisplayMinWidth: 2),
            ],
            WirePattern: "{year}-{month}-{day}",
            DisplayPattern: "{day}.{month}.{year}"));

        registry.Register(new LeafConstructorDefinition(
            WeekGrainLeafId,
            "Неделя",
            [
                new ConstructorSegmentDefinition("year", "Год"),
                new ConstructorSegmentDefinition("week", "Неделя", WireMinWidth: 2, DisplayMinWidth: 2),
            ],
            WirePattern: "{year}-W{week}",
            DisplayPattern: "W{week} {year}"));

        registry.Register(new LeafConstructorDefinition(
            MonthWeekGrainLeafId,
            "Неделя месяца",
            [
                new ConstructorSegmentDefinition("year", "Год"),
                new ConstructorSegmentDefinition("month", "Месяц", WireMinWidth: 2, DisplayMinWidth: 2),
                new ConstructorSegmentDefinition("month_week", "Неделя месяца"),
            ],
            WirePattern: "{year}-{month}-M{month_week}",
            DisplayPattern: "{month_week}-я нед. {month}.{year}"));

        registry.Register(new LeafConstructorDefinition(
            MonthGrainLeafId,
            "Месяц",
            [
                new ConstructorSegmentDefinition("year", "Год"),
                new ConstructorSegmentDefinition("month", "Месяц", WireMinWidth: 2, DisplayMinWidth: 2),
            ],
            WirePattern: "{year}-{month}",
            DisplayPattern: "{month}.{year}"));

        registry.Register(new LeafConstructorDefinition(
            QuarterGrainLeafId,
            "Квартал",
            [
                new ConstructorSegmentDefinition("year", "Год"),
                new ConstructorSegmentDefinition("quarter", "Квартал"),
            ],
            WirePattern: "{year}-{quarter}",
            DisplayPattern: "{quarter} {year}"));

        registry.Register(new CompositeConstructorDefinition(
            DateWeekId,
            "Неделя…",
            [
                new ConstructorSlotDefinition("value", WeekGrainLeafId, "Неделя"),
            ],
            WirePattern: "{value}"));

        registry.Register(new CompositeConstructorDefinition(
            DateMonthWeekId,
            "Неделя месяца…",
            [
                new ConstructorSlotDefinition("value", MonthWeekGrainLeafId, "Неделя месяца"),
            ],
            WirePattern: "{value}"));

        registry.Register(new CompositeConstructorDefinition(
            DateMonthId,
            "Месяц…",
            [
                new ConstructorSlotDefinition("value", MonthGrainLeafId, "Месяц"),
            ],
            WirePattern: "{value}"));

        registry.Register(new CompositeConstructorDefinition(
            DateQuarterId,
            "Квартал…",
            [
                new ConstructorSlotDefinition("value", QuarterGrainLeafId, "Квартал"),
            ],
            WirePattern: "{value}"));

        registry.Register(new CompositeConstructorDefinition(
            DateRangeId,
            "Период…",
            [
                new ConstructorSlotDefinition("from", DateLeafId, "Дата (с)"),
                new ConstructorSlotDefinition("to", DateLeafId, "Дата (по)", SeparatorBefore: ".."),
            ],
            WirePattern: "{from}..{to}"));
    }
}
