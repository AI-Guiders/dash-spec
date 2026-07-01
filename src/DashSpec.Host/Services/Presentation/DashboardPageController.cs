using DashSpec.Core.Compilation;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using DashSpec.Host.Plugins;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Dev;
using DashSpec.Host.Services.Models;
using DashSpec.Host.Services.Presentation;
using DashSpec.Host.Services.Rendering;
using Microsoft.AspNetCore.Components.Forms;

namespace DashSpec.Host.Services.Presentation;

/// <summary>Page-level UI state and refresh orchestration for the dashboard home page.</summary>
public sealed class DashboardPageController : IDisposable
{
    private readonly IDashboardSession _session;
    private readonly VizPluginRegistry _vizPlugins;
    private readonly DevSpecReloadNotifier? _reloadNotifier;
    private CancellationTokenSource? _dashboardApplyCts;

    public DashboardPageController(
        IDashboardSession session,
        VizPluginRegistry vizPlugins,
        IWebHostEnvironment environment,
        DevSpecReloadNotifier reloadNotifier)
    {
        _session = session;
        _vizPlugins = vizPlugins;
        if (environment.IsDevelopment())
        {
            _reloadNotifier = reloadNotifier;
            _reloadNotifier.Changed += OnDevSpecFileChanged;
        }
    }

    public event Action? Changed;

    /// <summary>Marshals UI updates to the Blazor renderer (set from the page component).</summary>
    public Func<Func<Task>, Task>? UiDispatcher { get; set; }

    public bool Loaded { get; private set; }
    public bool Busy { get; private set; }
    public bool Switching { get; private set; }
    public string? Error { get; private set; }
    public string? LoadedSpecSource { get; private set; }
    public IReadOnlyList<CardRenderResult> Cards => _cards;
    public Dictionary<string, DateOnly> DateFrom { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, DateOnly> DateTo { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, HashSet<string>> SelectedFields { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> TopLimits { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, IReadOnlyList<string>> FiltersToCards { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, PlacementDefinition> TabPlacements { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public string? ActiveTabId { get; private set; }

    public IDashboardSession Session => _session;

    private List<CardRenderResult> _cards = [];

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _session.LoadAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            LoadedSpecSource = _session.LoadedSpecSource;
            await InitializeDashboardStateAsync().ConfigureAwait(false);
            Loaded = true;
            Notify();
            await RefreshCardsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Loaded = false;
            Notify();
        }
    }

    public async Task OnSpecUploadedAsync(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file is null)
        {
            return;
        }

        Switching = true;
        Loaded = false;
        Error = null;
        _dashboardApplyCts?.Cancel();
        Notify();

        try
        {
            const long maxBytes = 2 * 1024 * 1024;
            await using var stream = file.OpenReadStream(maxBytes);
            await _session.LoadFromUploadAsync(stream, file.Name).ConfigureAwait(false);
            LoadedSpecSource = _session.LoadedSpecSource;
            await InitializeDashboardStateAsync().ConfigureAwait(false);
            Loaded = true;
            Notify();
            await RefreshCardsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Loaded = false;
            Notify();
        }
        finally
        {
            Switching = false;
            Notify();
        }
    }

    public void SelectTab(string tabId)
    {
        ActiveTabId = tabId;
        RecomputeTabPlacements();
        Notify();
    }

    public IEnumerable<CardRenderResult> VisibleCards()
    {
        if (_session.Document.Tabs.Count == 0 || string.IsNullOrWhiteSpace(ActiveTabId))
        {
            return _cards;
        }

        var tab = _session.Document.Tabs.Single(t =>
            string.Equals(t.Id, ActiveTabId, StringComparison.OrdinalIgnoreCase));

        return _cards.Where(card =>
            tab.CardIds.Contains(card.Id, StringComparer.OrdinalIgnoreCase));
    }

    public void OnDashboardFieldChanged((string Name, HashSet<string> Values) args)
    {
        SelectedFields[args.Name] = args.Values;
        ScheduleDashboardApplyAsync();
    }

    public void OnDashboardDayChanged(string name)
    {
        if (DateFrom.TryGetValue(name, out var day))
        {
            DateTo[name] = day;
        }

        ScheduleDashboardApplyAsync();
    }

    public void OnCardDayChanged((string CardId, string FilterName) args)
    {
        if (DateFrom.TryGetValue(args.FilterName, out var day))
        {
            DateTo[args.FilterName] = day;
        }

        ScheduleCardApplyAsync(args.CardId);
    }

    public void OnCardFieldChanged((string CardId, string FilterName, HashSet<string> Values) args)
    {
        SelectedFields[args.FilterName] = args.Values;
        ScheduleCardApplyAsync(args.CardId);
    }

    public void ScheduleDashboardApplyAsync()
    {
        if (!_session.Document.FiltersChrome.IsAutoApply)
        {
            return;
        }

        _dashboardApplyCts?.Cancel();
        _dashboardApplyCts?.Dispose();
        _dashboardApplyCts = new CancellationTokenSource();
        var token = _dashboardApplyCts.Token;
        _ = DebouncedDashboardApplyAsync(token);
    }

    public void ScheduleCardApplyAsync(string cardId)
    {
        if (!_session.Document.FiltersChrome.IsAutoApply)
        {
            return;
        }

        _ = ApplyCardFiltersAsync(cardId);
    }

    public async Task ApplyFiltersAsync()
    {
        foreach (var filterName in _session.Document.DashboardFilters)
        {
            var filter = _session.FilterIndex[filterName];
            if (filter.Kind is FilterKind.Date &&
                DateFrom.TryGetValue(filter.Name, out var from) &&
                DateTo.TryGetValue(filter.Name, out var to))
            {
                _session.ApplyDateFilter(filter.Name, from, to);
            }
            else if (filter.Kind is FilterKind.Field &&
                     SelectedFields.TryGetValue(filter.Name, out var selected))
            {
                _session.ApplyFieldFilter(filter.Name, selected);
            }
        }

        await RefreshCardsAsync().ConfigureAwait(false);
    }

    public async Task ApplyCardFiltersAsync(string cardId)
    {
        var card = _session.Document.Cards.Single(c =>
            string.Equals(c.Id, cardId, StringComparison.OrdinalIgnoreCase));

        foreach (var filterName in card.LocalFilters)
        {
            var filter = _session.FilterIndex[filterName];
            if (filter.Kind is FilterKind.Date &&
                DateFrom.TryGetValue(filter.Name, out var from) &&
                DateTo.TryGetValue(filter.Name, out var to))
            {
                _session.ApplyDateFilter(filter.Name, from, to);
            }
            else if (filter.Kind is FilterKind.Field &&
                     SelectedFields.TryGetValue(filter.Name, out var selected))
            {
                _session.ApplyFieldFilter(filter.Name, selected);
            }
            else if (filter.Kind is FilterKind.Top &&
                     TopLimits.TryGetValue(filter.Name, out var topLimit))
            {
                _session.ApplyTopFilter(filter.Name, TopLimitDefaults.Resolve(filter, topLimit));
            }
        }

        var index = _session.Document.Cards
            .Select((definition, i) => (definition, i))
            .Single(x => string.Equals(x.definition.Id, cardId, StringComparison.OrdinalIgnoreCase))
            .i;

        try
        {
            var result = await _session.RenderCardAsync(card).ConfigureAwait(false);
            _cards[index] = result;
        }
        catch (Exception ex)
        {
            _cards[index] = _cards[index] with { Error = ex.Message, Loading = false };
        }

        Notify();
    }

    public IReadOnlyList<string> DashboardBoundForCard(CardRenderResult card) =>
        (card.BoundFilters ?? [])
            .Where(name => _session.Document.DashboardFilters.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToList();

    public string FiltersSectionClass()
    {
        var chrome = _session.Document.FiltersChrome;
        var filtersClass = chrome.IsBarLayout ? "filters filters-bar" : "filters filters-card";
        if (chrome.IsStickyCard)
        {
            filtersClass += " filters-sticky-card";
        }

        return filtersClass;
    }

    public string FiltersGridClass() =>
        _session.Document.FiltersChrome.IsStickyLine
            ? "filters-grid filters-grid-sticky"
            : "filters-grid";

    public void Dispose()
    {
        if (_reloadNotifier is not null)
        {
            _reloadNotifier.Changed -= OnDevSpecFileChanged;
        }

        _dashboardApplyCts?.Cancel();
        _dashboardApplyCts?.Dispose();
    }

    private void OnDevSpecFileChanged() => _ = ReloadFromDiskAsync();

    private async Task ReloadFromDiskAsync()
    {
        if (UiDispatcher is null)
        {
            return;
        }

        await UiDispatcher(async () =>
        {
            Switching = true;
            Notify();
            try
            {
                await _session.LoadAsync().ConfigureAwait(false);
                LoadedSpecSource = _session.LoadedSpecSource;
                await InitializeDashboardStateAsync().ConfigureAwait(false);
                Error = null;
                Loaded = true;
                await RefreshCardsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                Loaded = false;
            }
            finally
            {
                Switching = false;
                Notify();
            }
        }).ConfigureAwait(false);
    }

    private async Task InitializeDashboardStateAsync()
    {
        ActiveTabId = _session.Document.Tabs.FirstOrDefault()?.Id;
        RecomputeTabPlacements();
        FiltersToCards = FilterBinding.MapFiltersToCards(_session.Document, _session.SpecLibrary)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

        DateFrom.Clear();
        DateTo.Clear();
        SelectedFields.Clear();
        TopLimits.Clear();

        foreach (var filterName in PlacedFilterNames())
        {
            var filter = _session.FilterIndex[filterName];
            if (filter.Kind is FilterKind.Date)
            {
                var range = _session.Filters.GetDate(filter.Name)
                    ?? DateDefaultRange.Resolve(filter.DefaultExpression!, DateOnly.FromDateTime(DateTime.UtcNow));
                DateFrom[filter.Name] = range.From;
                DateTo[filter.Name] = range.To;
            }
            else if (filter.Kind is FilterKind.Field)
            {
                var values = _session.Filters.GetField(filter.Name)?.Values
                    ?? FieldFilterDefaults.ResolveValues(filter.DefaultExpression);
                SelectedFields[filter.Name] = values.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            else if (filter.Kind is FilterKind.Top)
            {
                TopLimits[filter.Name] = TopLimitDefaults.Resolve(
                    filter,
                    _session.Filters.GetTop(filter.Name));
            }
        }

        await Task.CompletedTask;
    }

    private void RecomputeTabPlacements()
    {
        TabPlacements = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase);
        if (_session.Document.Tabs.Count == 0 || string.IsNullOrWhiteSpace(ActiveTabId))
        {
            return;
        }

        foreach (var (title, placement) in TabLayoutCompactor.Compact(
                     _session.Document,
                     ActiveTabId,
                     _session.SpecLibrary))
        {
            TabPlacements[title] = placement;
        }
    }

    private async Task DebouncedDashboardApplyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_session.Document.FiltersChrome.DebounceMs, cancellationToken).ConfigureAwait(false);
            await ApplyFiltersAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RefreshCardsAsync()
    {
        Busy = true;
        var dashboardFilters = _session.Document.DashboardFilters;
        _cards = _session.Document.Cards
            .Select(card => CardRenderSkeletonFactory.CreateLoading(
                card,
                _session.SpecLibrary,
                _vizPlugins,
                dashboardFilters))
            .ToList();
        Notify();

        var cardDefs = _session.Document.Cards.ToList();
        var results = new CardRenderResult[cardDefs.Count];

        await Task.WhenAll(cardDefs.Select(async (card, index) =>
        {
            try
            {
                results[index] = await _session.RenderCardAsync(card).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                results[index] = CardRenderSkeletonFactory.CreateError(
                    card,
                    _session.SpecLibrary,
                    _vizPlugins,
                    dashboardFilters,
                    ex.Message);
            }
        })).ConfigureAwait(false);

        async Task ApplyResultsAsync()
        {
            _cards = results.ToList();
            Busy = false;
            Notify();
        }

        if (UiDispatcher is not null)
        {
            await UiDispatcher(ApplyResultsAsync).ConfigureAwait(false);
        }
        else
        {
            await ApplyResultsAsync().ConfigureAwait(false);
        }
    }

    private IEnumerable<string> PlacedFilterNames() =>
        _session.Document.DashboardFilters
            .Concat(_session.Document.Cards.SelectMany(c => c.LocalFilters))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private void Notify() => Changed?.Invoke();
}
