#nullable enable
using AIGuiders.Platform.CommandPlane;

namespace DashSpec.Host.Commands;

/// <summary>Bundled slash catalog for filter commands (DASHSPEC-ADR-0043).</summary>
public static class DashboardFilterCommandCatalog
{
    public static SlashCatalogIndex Bundled { get; } = SlashCatalogIndex.FromDescriptors([
        new SlashCommandDescriptor
        {
            Domain = "dash", Object = "select", Intent = "date",
            CommandId = "dash.select.date",
            Path = "select date",
            Help = "Set date filter (today, last-week, YYYY-MM, range)",
            ArgTail = "required",
            Group = "Filters",
        },
        new SlashCommandDescriptor
        {
            Domain = "dash", Object = "select", Intent = "field",
            CommandId = "dash.select.field",
            Path = "select",
            PathAliases = ["select app", "select user"],
            Help = "Set field filter by alias",
            ArgTail = "required",
            Group = "Filters",
        },
    ]);

    public static SlashCatalogIndex ForDocument(IEnumerable<string> fieldAliases)
    {
        var descriptors = new List<SlashCommandDescriptor>
        {
            new()
            {
                Domain = "dash", Object = "select", Intent = "date",
                CommandId = "dash.select.date",
                Path = "select date",
                Help = "Set date filter",
                ArgTail = "required",
                Group = "Filters",
            },
        };

        foreach (var alias in fieldAliases.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            descriptors.Add(new SlashCommandDescriptor
            {
                Domain = "dash", Object = "select", Intent = alias.Trim(),
                CommandId = $"dash.select.{alias.Trim()}",
                Path = $"select {alias.Trim()}",
                Help = $"Set {alias.Trim()} filter",
                ArgTail = "required",
                Group = "Filters",
            });
        }

        return SlashCatalogIndex.FromDescriptors(descriptors);
    }
}
