namespace DashSpec.Host.Configuration;

internal static class HostBootstrapPlanetTomlGuard
{
    internal static void RejectLegacyPlanetToml(DashSpecTomlRoot bootstrap)
    {
        if (!string.IsNullOrWhiteSpace(bootstrap.Dashboard.CatalogPath))
        {
            throw new InvalidOperationException(
                "[dashboard] catalog_path is removed; set [host] dashhost to a .dashhost file.");
        }

        if (HasPresentationInToml(bootstrap.Presentation))
        {
            throw new InvalidOperationException(
                "[presentation] in dash-spec.toml is removed; use the presentation block in .dashhost.");
        }
    }

    internal static void RejectLegacyRuntimeLinks(DashSpecTomlRoot runtime, string configPath)
    {
        if (runtime.Links.Count > 0)
        {
            throw new InvalidOperationException(
                $"[[links]] in '{configPath}' are removed; define links in .dashhost.");
        }
    }

    private static bool HasPresentationInToml(PresentationTomlSection presentation) =>
        !string.IsNullOrWhiteSpace(presentation.Language)
        || !string.IsNullOrWhiteSpace(presentation.DisplayTimeZone)
        || !string.IsNullOrWhiteSpace(presentation.ColorScheme)
        || !string.IsNullOrWhiteSpace(presentation.LargeFieldFilterLayout);
}
