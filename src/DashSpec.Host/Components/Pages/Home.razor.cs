using DashSpec.Host.Services.Presentation;
using Microsoft.AspNetCore.Components;

namespace DashSpec.Host.Components.Pages;

public partial class Home : IDisposable
{
    [Inject] private DashboardPageController Page { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        Page.UiDispatcher = InvokeAsync;
        Page.Changed += OnPageChanged;
        await Page.InitializeAsync();
    }

    private void OnPageChanged() => _ = InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        Page.Changed -= OnPageChanged;
        Page.Dispose();
    }
}
