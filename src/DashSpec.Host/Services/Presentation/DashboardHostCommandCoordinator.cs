#nullable enable
using DashSpec.Core.Model;
using DashSpec.Host.Commands;
using DashSpec.Host.Configuration;
using Microsoft.AspNetCore.Components;

namespace DashSpec.Host.Services.Presentation;

/// <summary>Layout-level CCL bridge — dashboard context when loaded, host-only on Control Center.</summary>
public sealed class DashboardHostCommandCoordinator
{
    readonly NavigationManager _navigation;
    readonly DashboardFilterCommandService _commands;
    readonly DashboardFilterUiState _uiState;
    readonly IDashboardCultureAmbient _culture;
    readonly DashSpecHostContext _hostContext;

    DashboardPageController? _dashboard;
    string? _pendingCatalogEntryId;

    public DashboardHostCommandCoordinator(
        NavigationManager navigation,
        DashboardFilterCommandService commands,
        DashboardFilterUiState uiState,
        IDashboardCultureAmbient culture,
        DashSpecHostContext hostContext)
    {
        _navigation = navigation;
        _commands = commands;
        _uiState = uiState;
        _culture = culture;
        _hostContext = hostContext;
    }

    public event Action? Changed;

    public string? CommandError { get; private set; }

    public bool HasCatalog => _hostContext.Catalog.Document.Entries.Count > 1;

    public IReadOnlyList<CatalogEntryDefinition> CatalogEntries =>
        _hostContext.Catalog.Document.Entries;

    public IReadOnlyList<CatalogGroupDefinition> CatalogGroups =>
        _hostContext.Catalog.Document.Groups ?? [];

    public string ActiveCatalogEntryId => ResolveActiveCatalogEntryId();

    public bool CatalogBusy => _dashboard?.Switching == true;

    public async Task SelectCatalogEntryAsync(string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId))
        {
            return;
        }

        if (_dashboard is { Loaded: true })
        {
            _pendingCatalogEntryId = entryId;
            Notify();
            try
            {
                await _dashboard.SelectCatalogEntryAsync(entryId).ConfigureAwait(false);
                CommandError = _dashboard.CommandError;
                NavigateToReport(entryId, replace: true);
            }
            finally
            {
                _pendingCatalogEntryId = null;
                Notify();
            }

            return;
        }

        NavigateToReport(entryId, replace: false);
    }

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

    string ResolveActiveCatalogEntryId()
    {
        if (!string.IsNullOrWhiteSpace(_pendingCatalogEntryId))
        {
            return _pendingCatalogEntryId;
        }

        if (!string.IsNullOrWhiteSpace(_dashboard?.ActiveCatalogEntryId))
        {
            return _dashboard.ActiveCatalogEntryId;
        }

        var fromUri = ResolveReportFromUri();
        if (!string.IsNullOrWhiteSpace(fromUri)
            && CatalogEntries.Any(entry => string.Equals(entry.Id, fromUri, StringComparison.OrdinalIgnoreCase)))
        {
            return fromUri;
        }

        return _hostContext.Catalog.Document.DefaultEntryId;
    }

    string? ResolveReportFromUri()
    {
        if (!Uri.TryCreate(_navigation.Uri, UriKind.Absolute, out var navUri))
        {
            return null;
        }

        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(navUri.Query);
        return query.TryGetValue("report", out var values) ? values.ToString() : null;
    }

    void NavigateToReport(string entryId, bool replace)
    {
        if (!Uri.TryCreate(_navigation.Uri, UriKind.Absolute, out var navUri))
        {
            _navigation.NavigateTo($"/?report={Uri.EscapeDataString(entryId)}", replace);
            return;
        }

        var qs = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(navUri.Query);
        var pairs = new List<KeyValuePair<string, string?>>();
        foreach (var kv in qs)
        {
            if (!string.Equals(kv.Key, "report", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var v in kv.Value) { pairs.Add(new(kv.Key, v)); }
            }
        }

        pairs.Add(new("report", entryId));
        var targetPath = navUri.GetComponents(UriComponents.Path, UriFormat.Unescaped);
        var rebuilt = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(targetPath, pairs);
        _navigation.NavigateTo(rebuilt, replace);
    }

    void Notify() => Changed?.Invoke();
}
