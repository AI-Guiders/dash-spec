#nullable enable

namespace DashSpec.Host.Commands;

internal static class ShowCommandPaths
{
    public const string RootVerb = "show";
    public const string HostBranch = "host";

    public static string SurfacePath(string surfaceId) =>
        DashboardCatalogPhrases.Materialize(
            DashboardCatalogPhrases.ShowHostPhrase,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["surface"] = surfaceId,
            });

    public static string? ReadSurfaceId(string canonicalPath) =>
        DashboardCatalogPhrases.TryReadSlot(
            DashboardCatalogPhrases.ShowHostPhrase,
            canonicalPath,
            "surface",
            out var surfaceId)
            ? surfaceId
            : null;
}
