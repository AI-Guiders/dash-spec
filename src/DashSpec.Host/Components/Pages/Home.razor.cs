using DashSpec.Host.Components.Dashboard;
using DashSpec.Host.Services.Presentation;
using Microsoft.AspNetCore.Components;

namespace DashSpec.Host.Components.Pages;

public partial class Home : IDisposable
{
    [Inject] private DashboardPageController Page { get; set; } = default!;
    [Inject] private DashboardHostCommandCoordinator CommandCoordinator { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        CommandCoordinator.AttachDashboard(Page);
        Page.UiDispatcher = InvokeAsync;
        Page.Changed += OnPageChanged;
        string? requestedEntry = null;
        if (Uri.TryCreate(Navigation.Uri, UriKind.Absolute, out var navUri))
        {
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(navUri.Query);
            if (query.TryGetValue("report", out var vals)) { requestedEntry = vals.ToString(); }
        }

        await Page.InitializeAsync(requestedCatalogEntryId: requestedEntry);
        await InvokeAsync(StateHasChanged);
    }

    private void OnPageChanged() => _ = InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        Page.Changed -= OnPageChanged;
        CommandCoordinator.DetachDashboard();
    }
}
