namespace DashSpec.Host.Configuration;

internal static class HostBootstrapPlanetTomlGuard
{
    internal static void RejectLegacyPlanetToml(DashSpecTomlRoot bootstrap, string? configPath = null)
    {
        var where = FormatConfigPath(configPath);

        if (!string.IsNullOrWhiteSpace(bootstrap.Dashboard.CatalogPath))
        {
            throw new InvalidOperationException(
                $"[dashboard] catalog_path in {where} is removed; set [host] dashhost to a .dashhost file.");
        }

        if (HasPresentationInToml(bootstrap.Presentation))
        {
            throw new InvalidOperationException(
                $"[presentation] in {where} is removed; use the presentation block in .dashhost.");
        }

        RejectLegacyLinks(bootstrap, configPath);
    }

    internal static void RejectLegacyRuntimeLinks(DashSpecTomlRoot runtime, string configPath) =>
        RejectLegacyLinks(runtime, configPath);

    private static void RejectLegacyLinks(DashSpecTomlRoot root, string? configPath)
    {
        if (root.Links.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"[[links]] in {FormatConfigPath(configPath)} are removed; define links in .dashhost.");
    }

    private static string FormatConfigPath(string? configPath) =>
        string.IsNullOrWhiteSpace(configPath) ? "dash-spec TOML" : $"'{configPath}'";

    private static bool HasPresentationInToml(PresentationTomlSection presentation) =>
        !string.IsNullOrWhiteSpace(presentation.Language)
        || !string.IsNullOrWhiteSpace(presentation.DisplayTimeZone)
        || !string.IsNullOrWhiteSpace(presentation.ColorScheme)
        || !string.IsNullOrWhiteSpace(presentation.LargeFieldFilterLayout);
}
