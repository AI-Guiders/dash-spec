using DashSpec.Surface.Blazor.Components.Dashboard;
using DashSpec.Surface.Blazor.Services.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace DashSpec.Surface.Blazor.Components.Pages;

public partial class Home : IDisposable
{
    [Inject] private DashboardPageController Page { get; set; } = default!;
    [Inject] private DashboardHostCommandCoordinator CommandCoordinator { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private DashSpec.Viz.ICardChromeControlsOverride ChromeControls { get; set; } = default!;

    [Inject] private DashSpec.Abstractions.Viewer.IViewerShellChrome HostShell { get; set; } = default!;

    private bool HasSidebarChrome => ChromeControls.ResolveSidebar() is not null;

    protected override async Task OnInitializedAsync()
    {
        Page.SyncBrowserUrl = true;
        CommandCoordinator.AttachDashboard(Page);
        Page.UiDispatcher = InvokeAsync;
        Page.Changed += OnPageChanged;
        Navigation.LocationChanged += OnLocationChanged;

        ParseQuery(Navigation.Uri, out var requestedEntry, out var requestedTab, out var requestedPage);

        await Page.InitializeAsync(requestedCatalogEntryId: requestedEntry);
        if (!string.IsNullOrWhiteSpace(requestedTab) || !string.IsNullOrWhiteSpace(requestedPage))
        {
            await Page.ApplyLocationQueryAsync(requestedEntry, requestedTab, requestedPage);
        }

        await InvokeAsync(StateHasChanged);
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        ParseQuery(e.Location, out var requestedEntry, out var requestedTab, out var requestedPage);
        _ = InvokeAsync(async () =>
        {
            await Page.ApplyLocationQueryAsync(requestedEntry, requestedTab, requestedPage);
            StateHasChanged();
        });
    }

    private static void ParseQuery(string uri, out string? report, out string? tab, out string? page)
    {
        report = null;
        tab = null;
        page = null;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var navUri))
        {
            return;
        }

        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(navUri.Query);
        if (query.TryGetValue("report", out var rVals)) { report = rVals.ToString(); }
        if (query.TryGetValue("tab", out var tVals)) { tab = tVals.ToString(); }
        if (query.TryGetValue("page", out var pVals)) { page = pVals.ToString(); }
    }

    private void OnPageChanged() => _ = InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        Page.Changed -= OnPageChanged;
        CommandCoordinator.DetachDashboard();
    }
}
