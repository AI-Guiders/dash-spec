using DashSpec.Host.Components.Dashboard;
using DashSpec.Host.Services.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DashSpec.Host.Components.Pages;

public partial class Home : IDisposable
{
    [Inject] private DashboardPageController Page { get; set; } = default!;

    DashboardFilterCommandBar? _filterCommandBar;
    DashboardCommandHighlightRegion? _commandHighlightRegion;

    async Task OnCommandTailChangedAsync(string tail)
    {
        if (_commandHighlightRegion is not null)
        {
            await _commandHighlightRegion.NotifyTailChangedAsync(tail);
        }
    }

    void OnWindowKeyDown(KeyboardEventArgs e)
    {
        if (!Page.Loaded)
        {
            return;
        }

        if (e.CtrlKey && string.Equals(e.Key, "k", StringComparison.OrdinalIgnoreCase))
        {
            _filterCommandBar?.OpenPaletteFromHost();
        }
    }

    protected override async Task OnInitializedAsync()
    {
        Page.UiDispatcher = InvokeAsync;
        Page.Changed += OnPageChanged;
        await Page.InitializeAsync();
        await InvokeAsync(StateHasChanged);
    }

    private void OnPageChanged() => _ = InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        Page.Changed -= OnPageChanged;
        Page.Dispose();
    }
}
