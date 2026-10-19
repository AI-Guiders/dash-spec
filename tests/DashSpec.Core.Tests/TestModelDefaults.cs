namespace DashSpec.Core.Tests;

internal static class TestModelDefaults
{
    public static FiltersChromeDefinition FiltersChrome => new()
    {
        Layout = "card",
        Sticky = "none",
        Apply = "manual",
        ApplyControl = "icon",
        DebounceMs = 400,
        FormatGuide = "hidden",
        Cells = "",
    };
}
