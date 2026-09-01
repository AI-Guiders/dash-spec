#nullable enable
using DashSpec.Host.Commands;
using Microsoft.AspNetCore.Components;

namespace DashSpec.Host.Services.Presentation;

/// <summary>Layout-level CCL bridge — dashboard context when loaded, host-only on Control Center.</summary>
public sealed class DashboardHostCommandCoordinator
{
    readonly NavigationManager _navigation;
    readonly DashboardFilterCommandService _commands;
    readonly DashboardFilterUiState _uiState;
    readonly IDashboardCultureAmbient _culture;

    DashboardPageController? _dashboard;

    public DashboardHostCommandCoordinator(
        NavigationManager navigation,
        DashboardFilterCommandService commands,
        DashboardFilterUiState uiState,
        IDashboardCultureAmbient culture)
    {
        _navigation = navigation;
        _commands = commands;
        _uiState = uiState;
        _culture = culture;
    }

    public event Action? Changed;

    public string? CommandError { get; private set; }

    public void AttachDashboard(DashboardPageController dashboard)
    {
        if (_dashboard is not null)
        {
            _dashboard.Changed -= OnDashboardChanged;
        }

        _dashboard = dashboard;
        _dashboard.Changed += OnDashboardChanged;
        Notify();
    }

    public void DetachDashboard()
    {
        if (_dashboard is not null)
        {
            _dashboard.Changed -= OnDashboardChanged;
            _dashboard = null;
        }

        Notify();
    }

    void OnDashboardChanged() => Notify();

    public DashboardFilterContext BuildContext() =>
        _dashboard is { Loaded: true }
            ? _dashboard.BuildCommandContext()
            : HostCommandContextFactory.CreateHostOnly(_uiState, _culture);

    public async Task CommitAsync(string line)
    {
        CommandError = null;
        if (_dashboard is { Loaded: true })
        {
            await _dashboard.OnFilterCommandCommittedAsync(line).ConfigureAwait(false);
            CommandError = _dashboard.CommandError;
            Notify();
            return;
        }

        var context = BuildContext();
        var run = _commands.TryExecute(line, context);
        if (!run.Outcome.Success)
        {
            CommandError = run.Outcome.Error;
            Notify();
            return;
        }

        if (run.PendingHostRoute is not null)
        {
            _navigation.NavigateTo(run.PendingHostRoute);
            return;
        }

        CommandError = "Команда доступна на Dashboard.";
        Notify();
    }

    void Notify() => Changed?.Invoke();
}
