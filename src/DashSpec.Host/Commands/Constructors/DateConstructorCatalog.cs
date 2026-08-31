#nullable enable

using AIGuiders.Platform.CommandPlane;

namespace DashSpec.Host.Commands.Constructors;

internal static class DateConstructorCatalog
{
    public const string DateLeafId = "date";
    public const string DateRangeId = "date_range";

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
