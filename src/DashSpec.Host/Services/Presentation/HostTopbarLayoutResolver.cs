using DashSpec.Core.Model;

namespace DashSpec.Host.Services.Presentation;

/// <summary>Flatten <c>scope host</c> layout board into topbar slot order (ADR-0054).</summary>
public static class HostTopbarLayoutResolver
{
    public static readonly IReadOnlyList<string> DefaultSlots =
    [
        HostTopbarSlots.Catalog,
        HostTopbarSlots.Nav,
        HostTopbarSlots.ExternalLinks,
        HostTopbarSlots.Help,
        HostTopbarSlots.Settings,
        HostTopbarSlots.SpecDev,
    ];

    private static readonly HashSet<string> KnownSlots = new(StringComparer.OrdinalIgnoreCase)
    {
        HostTopbarSlots.Catalog,
        HostTopbarSlots.ReportPicker,
        HostTopbarSlots.Nav,
        HostTopbarSlots.Dashboard,
        HostTopbarSlots.ExternalLinks,
        HostTopbarSlots.Help,
        HostTopbarSlots.Settings,
        HostTopbarSlots.SpecDev,
    };

    public static IReadOnlyList<string> Resolve(LayoutBoardDefinition? board)
    {
        if (board is null || board.Rows.Count == 0)
        {
            return DefaultSlots;
        }

        var slots = new List<string>();
        foreach (var row in board.Rows)
        {
            foreach (var token in row)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                var normalized = Normalize(token);
                if (KnownSlots.Contains(normalized))
                {
                    slots.Add(normalized);
                }
            }
        }

        return slots.Count > 0 ? slots : DefaultSlots;
    }

    public static string Normalize(string token)
    {
        var value = token.Trim();
        if (value.Equals(HostTopbarSlots.ReportPicker, StringComparison.OrdinalIgnoreCase))
        {
            return HostTopbarSlots.Catalog;
        }

        if (value.Equals(HostTopbarSlots.Dashboard, StringComparison.OrdinalIgnoreCase))
        {
            return HostTopbarSlots.Nav;
        }

        return value.ToLowerInvariant();
    }
}

public static class HostTopbarSlots
{
    public const string Catalog = "catalog";
    public const string ReportPicker = "report_picker";
    public const string Nav = "nav";
    public const string Dashboard = "dashboard";
    public const string ExternalLinks = "external_links";
    public const string Help = "help";
    public const string Settings = "settings";
    public const string SpecDev = "spec_dev";
}
