using Microsoft.Extensions.Logging;

namespace DashSpec.Host.Configuration;

internal static class HostBootstrapDeprecation
{
    internal static void WarnPlanetTomlOverrides(
        ILogger? logger,
        bool hadTomlCatalogPath,
        bool hadTomlPresentation,
        HostShellBootstrap? hostShell)
    {
        if (logger is null || hostShell is null)
        {
            return;
        }

        if (hadTomlCatalogPath)
        {
            logger.LogWarning(
                "[dashboard] catalog_path in dash-spec.toml is deprecated; catalog is defined in {DashhostPath}.",
                hostShell.FullPath);
        }

        if (hadTomlPresentation)
        {
            logger.LogWarning(
                "[presentation] in dash-spec.toml is deprecated; host chrome is defined in {DashhostPath}.",
                hostShell.FullPath);
        }
    }

    internal static void WarnRuntimeLinks(ILogger? logger, HostShellBootstrap? hostShell, DashSpecTomlRoot runtime)
    {
        if (logger is null || hostShell is null || hostShell.Document.Links.Count == 0)
        {
            return;
        }

        if (runtime.Links.Count > 0)
        {
            logger.LogWarning(
                "[[links]] in @runtime TOML are deprecated; external links are defined in {DashhostPath}.",
                hostShell.FullPath);
        }
    }

    internal static bool HasPresentationInToml(PresentationTomlSection presentation) =>
        !string.IsNullOrWhiteSpace(presentation.Language)
        || !string.IsNullOrWhiteSpace(presentation.DisplayTimeZone)
        || !string.IsNullOrWhiteSpace(presentation.ColorScheme)
        || !string.IsNullOrWhiteSpace(presentation.LargeFieldFilterLayout);
}
