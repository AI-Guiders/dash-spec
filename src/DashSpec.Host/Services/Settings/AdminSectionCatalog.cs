namespace DashSpec.Host.Services.Settings;

/// <summary>Control Center sections — List pane (ADR-0042), Forge/GH settings shape.</summary>
public static class AdminSectionCatalog
{
    public const string DefaultSectionId = "access";

    public static IReadOnlyList<AdminSection> All { get; } =
    [
        new("access", "Access", "Host API key"),
        new("catalog", "Catalog", "Git catalog clone / poll"),
        new("sync", "Sync webhook", "Inbound push URL + HMAC secret"),
        new("export", "Export", "TOML fragment for air-gap backup"),
    ];

    public static bool TryResolve(string? sectionId, out AdminSection section)
    {
        var id = string.IsNullOrWhiteSpace(sectionId) ? DefaultSectionId : sectionId.Trim();
        foreach (var item in All)
        {
            if (string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                section = item;
                return true;
            }
        }

        section = All[0];
        return false;
    }
}

public sealed record AdminSection(string Id, string Title, string Description);
