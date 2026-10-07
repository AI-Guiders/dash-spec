using DashSpec.Abstractions.Viewer;
using DashSpec.Host.Services.Dev;
using DashSpec.Surface.Blazor.Services.Presentation;
using DashSpec.Host.Services.Settings;

namespace DashSpec.Host.Configuration;

internal sealed class CatalogUsageClientAdapter(CatalogUsageService inner) : ICatalogUsageClient
{
    public string GetOrCreateClientId(HttpContext? httpContext) => inner.GetOrCreateClientId(httpContext);

    public string? ResolvePreferredEntryId(string clientId, string catalogDefaultEntryId) =>
        inner.ResolvePreferredEntryId(clientId, catalogDefaultEntryId);

    public void RecordSelection(string clientId, string entryId) => inner.RecordSelection(clientId, entryId);
}

internal sealed class DevSpecReloadSignalAdapter(DevSpecReloadNotifier inner) : IDevSpecReloadSignal
{
    public event Action? Changed
    {
        add => inner.Changed += value;
        remove => inner.Changed -= value;
    }
}

internal sealed class ViewerExternalLinksProviderAdapter(HostExternalLinksProvider inner) : IViewerExternalLinksProvider
{
    public IReadOnlyList<ViewerExternalLink> ForStartupRuntime() =>
        inner.ForStartupRuntime()
            .Select(link => new ViewerExternalLink(link.Label, link.Url, link.Target, link.Topbar, link.Settings))
            .ToList();
}

internal sealed class ViewerShellChromeAdapter(HostShellBootstrap shell) : IViewerShellChrome
{
    public string ProductTitle => shell.ProductTitle;

    public string CatalogLabel => shell.CatalogLabel;

    public IReadOnlyList<string> TopbarSlots => shell.TopbarSlots;

    public bool ShowsSurface(string surface) => shell.ShowsSurface(surface);
}

internal sealed class ViewerPresentationOptionsAdapter(DashSpecTomlRoot bootstrap) : IViewerPresentationOptions
{
    public string? Language => bootstrap.Presentation.Language;

    public string ColorScheme => bootstrap.Presentation.ColorScheme;

    public string LargeFieldFilterLayout => bootstrap.Presentation.LargeFieldFilterLayout;
}
