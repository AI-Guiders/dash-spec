#nullable enable

namespace DashSpec.Host.Commands;

/// <summary>Nav-agnostic host surfaces — extend here when adding tabs/routes.</summary>
public static class HostSurfaceCatalog
{
    public sealed record HostSurface(string Id, string Route, string Title, string Hint);

    public static IReadOnlyList<HostSurface> Surfaces { get; } =
    [
        new("dashboard", "/", "Dashboard", "Отчёты, фильтры и карточки"),
        new("controlcenter", "/admin/access", "Control Center", "Настройки хоста и каталога"),
    ];

    public static bool TryResolveRoute(string surfaceId, out string route)
    {
        route = "";
        var surface = Surfaces.FirstOrDefault(item =>
            string.Equals(item.Id, surfaceId, StringComparison.OrdinalIgnoreCase));
        if (surface is null)
        {
            return false;
        }

        route = surface.Route;
        return true;
    }
}
