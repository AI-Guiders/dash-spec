using DashSpec.Abstractions.Plugins;
using DashSpec.Core.Compilation;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Runtime;
using DashSpec.Host.Configuration;
using DashSpec.Host.Plugins;
using DashSpec.Host.Plugins.Builtins;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Dev;
using DashSpec.Host.Services.Diagnostics;
using DashSpec.Host.Services.Loading;
using DashSpec.Host.Services.Models;
using DashSpec.Host.Services.Presentation;
using DashSpec.Host.Services.Rendering;
using Microsoft.AspNetCore.Components.Forms;

namespace DashSpec.Host.Services.Presentation;

/// <summary>Page-level UI state for the dashboard home page.</summary>
public sealed class DashboardPageController : IDisposable
{
    private readonly IDashboardSession _session;
    private readonly OnClickInteractionService _interactions;
    private readonly DashSpecActionDispatcher _actions;
    private readonly ICardViewState _cardViewState;
    private readonly DashSpecHostContext _hostContext;
    private readonly DashboardFilterUiState _filters;
    private readonly DashboardRefreshCoordinator _refresh;
    private readonly DevSpecReloadNotifier? _reloadNotifier;
    private readonly LoadTrace _loadTrace;
    private readonly ILogger<DashboardPageController> _logger;

    public DashboardPageController(
        IDashboardSession session,
        OnClickInteractionService interactions,
        DashSpecActionDispatcher actions,
        ICardViewState cardViewState,
        DashSpecHostContext hostContext,
        DashboardFilterUiState filters,
        DashboardRefreshCoordinator refresh,
        IWebHostEnvironment environment,
        DevSpecReloadNotifier reloadNotifier,
        LoadTrace loadTrace,
        ILogger<DashboardPageController> logger)
    {
        _session = session;
        _interactions = interactions;
        _actions = actions;
        _cardViewState = cardViewState;
        _hostContext = hostContext;
        _filters = filters;
        _refresh = refresh;
        _loadTrace = loadTrace;
        _logger = logger;
        _refresh.StateChanged += OnRefreshStateChanged;
        if (environment.IsDevelopment())
        {
            _reloadNotifier = reloadNotifier;
            _reloadNotifier.Changed += OnDevSpecFileChanged;
        }
    }

    public event Action? Changed;

    /// <summary>Marshals UI updates to the Blazor renderer (set from the page component).</summary>
    public Func<Func<Task>, Task>? UiDispatcher
    {
        get => _refresh.UiDispatcher;
        set => _refresh.UiDispatcher = value;
    }

    public bool Loaded { get; private set; }
    public bool Busy => _refresh.Busy;
    public bool Switching { get; private set; }
    public string? Error { get; private set; }
    public string? LoadedSpecSource { get; private set; }
    public IReadOnlyList<CardRenderResult> Cards => _refresh.Cards;
    public DashboardFilterUiState FilterState => _filters;
    public Dictionary<string, DateOnly> DateFrom => _filters.DateFrom;
    public Dictionary<string, DateOnly> DateTo => _filters.DateTo;
    public Dictionary<string, HashSet<string>> SelectedFields => _filters.SelectedFields;
    public Dictionary<string, int> TopLimits => _filters.TopLimits;
    public Dictionary<string, IReadOnlyList<string>> FiltersToCards { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, PlacementDefinition> TabPlacements { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, PlacementDefinition> ToolbarPlacements { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public string? ActiveTabId { get; private set; }

    public bool HasCatalog => CatalogEntries.Count > 1;

    public IReadOnlyList<CatalogEntryDefinition> CatalogEntries =>
        _hostContext.Catalog.Document.Entries;

    public string? ActiveCatalogEntryId => _session.ActiveCatalogEntryId;

    public IDashboardSession Session => _session;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var trace = _loadTrace.Begin("ui:initialize");
        var fastLoad = new SpecLoadOptions { LoadFieldOptions = false };

        try
        {
            var loadSw = System.Diagnostics.Stopwatch.StartNew();
            await _session.LoadAsync(cancellationToken: cancellationToken, options: fastLoad).ConfigureAwait(false);
            loadSw.Stop();
            trace.Step("load_spec", loadSw.ElapsedMilliseconds, true, _session.LoadedSpecSource);

            LoadedSpecSource = _session.LoadedSpecSource;

            var stateSw = System.Diagnostics.Stopwatch.StartNew();
            await InitializeDashboardStateAsync().ConfigureAwait(false);
            stateSw.Stop();
            trace.Step("init_ui_state", stateSw.ElapsedMilliseconds, true);

            Loaded = true;
            Notify();

            _ = RefreshCardsInBackgroundAsync();
            trace.Succeed();
            _ = RefreshFieldOptionsInBackgroundAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard initialize failed");
            trace.Fail(ex.Message);
            Error = ex.Message;
            Loaded = false;
            Notify();
        }
    }

    private async Task RefreshCardsInBackgroundAsync()
    {
        using var trace = _loadTrace.Begin("ui:refresh_cards");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await _refresh.RefreshAllAsync().ConfigureAwait(false);
            sw.Stop();
            trace.Step("render", sw.ElapsedMilliseconds, true, $"{_session.Document.Cards.Count} cards");
            trace.Succeed();
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Card refresh failed");
            trace.Step("render", sw.ElapsedMilliseconds, false, error: ex.Message);
            trace.Fail(ex.Message);
        }
    }

    private async Task RefreshFieldOptionsInBackgroundAsync()
    {
        using var trace = _loadTrace.Begin("ui:field_options");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await _session.RefreshFieldOptionsAsync().ConfigureAwait(false);
            sw.Stop();
            trace.Step("load", sw.ElapsedMilliseconds, true);
            trace.Succeed();
            Notify();
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Background field options load failed");
            trace.Step("load", sw.ElapsedMilliseconds, false, error: ex.Message);
            trace.Fail(ex.Message);
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
        _refresh.CancelPendingApplies();
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
            _ = RefreshCardsInBackgroundAsync();
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

    public async Task SelectTab(string tabId)
    {
        ActiveTabId = tabId;
        RecomputeTabPlacements();
        RecomputeToolbarPlacements();
        Notify();
        await Task.CompletedTask;
    }

    public async Task ApplyCardClickNavigationAsync(
        CardRenderResult card,
        HeatmapCellContext context,
        CancellationToken cancellationToken = default)
    {
        if (card.ClickBehaviour is null)
        {
            return;
        }

        var navigate = false;
        foreach (var effect in _interactions.ExpandClickEffects(card.ClickBehaviour.Effects))
        {
            switch (effect)
            {
                case SetFilterFromFieldEffect setEffect:
                    ApplyFilterFromHeatmapCell(setEffect, context);
                    navigate = true;
                    break;
                case GotoTabEffect gotoEffect:
                    ActiveTabId = gotoEffect.TabId;
                    RecomputeTabPlacements();
                    RecomputeToolbarPlacements();
                    navigate = true;
                    break;
                case InvokeHandlerEffect invoke:
                    await _actions.ExecuteAsync(
                        invoke.HandlerId,
                        card,
                        invoke.Args,
                        context,
                        cancellationToken).ConfigureAwait(false);
                    break;
            }
        }

        if (!navigate)
        {
            return;
        }

        Notify();
        await ApplyFiltersAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task ApplyCardClickNavigationAsync((CardRenderResult Card, HeatmapCellContext Context) args) =>
        ApplyCardClickNavigationAsync(args.Card, args.Context);

    public Task ExecuteCardActionAsync(CardActionRequest request) =>
        ExecuteCardActionAsync(request, CancellationToken.None);

    public async Task ExecuteCardActionAsync(
        CardActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var card = _refresh.Cards.FirstOrDefault(c =>
            string.Equals(c.Id, request.CardId, StringComparison.OrdinalIgnoreCase));
        if (card is null)
        {
            return;
        }

        var outcome = await _actions.ExecuteAsync(
            request.ActionId,
            card,
            request.Args,
            clickContext: null,
            cancellationToken).ConfigureAwait(false);

        if (outcome.Kind is DashSpecActionOutcomeKind.RefreshCard &&
            !string.IsNullOrWhiteSpace(outcome.RefreshCardId))
        {
            await _refresh.RefreshSingleCardAsync(outcome.RefreshCardId, cancellationToken).ConfigureAwait(false);
        }
    }

    public string? GetCardActiveView(string cardId)
    {
        var stored = _cardViewState.GetActiveView(cardId);
        if (!string.IsNullOrWhiteSpace(stored))
        {
            return stored;
        }

        var card = _session.Document.Cards.FirstOrDefault(c =>
            string.Equals(c.Id, cardId, StringComparison.OrdinalIgnoreCase));
        return card is null ? null : CardViewSwitchApplier.ResolveDefaultViewId(card.ExtensionBlocks);
    }

    private void ApplyFilterFromHeatmapCell(SetFilterFromFieldEffect effect, HeatmapCellContext context)
    {
        if (!_session.FilterIndex.TryGetValue(effect.FilterName, out var filter))
        {
            return;
        }

        var raw = effect.Field switch
        {
            "x" => context.XLabel,
            "y" => context.YLabel,
            "value" => context.Value?.ToString("0") ?? string.Empty,
            _ => string.Empty,
        };

        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        switch (filter.Kind)
        {
            case FilterKind.Date when DateOnly.TryParse(raw, out var day):
                DateFrom[effect.FilterName] = day;
                DateTo[effect.FilterName] = day;
                break;
            case FilterKind.Field:
                SelectedFields[effect.FilterName] = new HashSet<string>([raw], StringComparer.OrdinalIgnoreCase);
                break;
        }
    }

    public bool ToolbarGroupBreakBefore(string filterName)
    {
        if (!GrainFilterPresentation.IsGrainHostFilter(filterName, _session.FilterIndex))
        {
            return false;
        }

        var ordered = VisibleToolbarFilterNames();
        var index = -1;
        for (var i = 0; i < ordered.Count; i++)
        {
            if (string.Equals(ordered[i], filterName, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }
        if (index <= 0)
        {
            return false;
        }

        return ordered.Take(index).Any(name =>
            _session.FilterIndex.TryGetValue(name, out var filter) &&
            filter.Kind is FilterKind.Date or FilterKind.Field);
    }

    public IReadOnlyList<string> VisibleToolbarFilters()
    {
        var visible = VisibleToolbarFilterNames();
        return visible
            .OrderBy(name => ToolbarPlacements.TryGetValue(name, out var placement) ? placement.Row : int.MaxValue)
            .ThenBy(name => ToolbarPlacements.TryGetValue(name, out var placement) ? placement.Col : int.MaxValue)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task SelectCatalogEntryAsync(string entryId)
    {
        if (string.Equals(_session.ActiveCatalogEntryId, entryId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Switching = true;
        Loaded = false;
        Error = null;
        _refresh.CancelPendingApplies();
        Notify();

        try
        {
            await _session.LoadCatalogEntryAsync(entryId).ConfigureAwait(false);
            LoadedSpecSource = _session.LoadedSpecSource;
            await InitializeDashboardStateAsync().ConfigureAwait(false);
            Loaded = true;
            Notify();
            _ = RefreshCardsInBackgroundAsync();
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

    public IEnumerable<CardRenderResult> VisibleCards()
    {
        if (_session.Document.Tabs.Count == 0 || string.IsNullOrWhiteSpace(ActiveTabId))
        {
            return _refresh.Cards;
        }

        var tab = _session.Document.Tabs.Single(t =>
            string.Equals(t.Id, ActiveTabId, StringComparison.OrdinalIgnoreCase));

        return _refresh.Cards.Where(card =>
            tab.CardIds.Contains(card.Id, StringComparer.OrdinalIgnoreCase));
    }

    public void OnDashboardFieldChanged((string Name, HashSet<string> Values) args)
    {
        SelectedFields[args.Name] = args.Values;
        NormalizeGrainDates(args.Name);
        _refresh.ScheduleDashboardApply();
    }

    public void OnDashboardDayChanged(string name)
    {
        if (!DateFrom.TryGetValue(name, out var day) || !SqlDateTimeRange.IsQueryable(day))
        {
            return;
        }

        DateTo[name] = day;
        _refresh.ScheduleDashboardApply();
    }

    public void OnCardDayChanged((string CardId, string FilterName) args)
    {
        if (DateFrom.TryGetValue(args.FilterName, out var day))
        {
            if (!SqlDateTimeRange.IsQueryable(day))
            {
                return;
            }

            DateTo[args.FilterName] = day;
        }

        if (!IsCardLocalManualApply(args.CardId))
        {
            _refresh.ScheduleCardApply(args.CardId);
        }
    }

    public void OnCardFieldChanged((string CardId, string FilterName, HashSet<string> Values) args)
    {
        SelectedFields[args.FilterName] = args.Values;
        NormalizeGrainDates(args.FilterName);
        if (!IsCardLocalManualApply(args.CardId))
        {
            _refresh.ScheduleCardApply(args.CardId);
        }
    }

    public void OnDashboardRangeChanged()
    {
        SnapAllGrainAnchoredDates();
        _refresh.ScheduleDashboardApply();
    }

    public void OnDashboardTopChanged((string Name, int Value) args)
    {
        TopLimits[args.Name] = args.Value;
        _refresh.ScheduleDashboardApply();
    }

    public void ScheduleDashboardApplyAsync() => _refresh.ScheduleDashboardApply();

    public void ScheduleCardApplyAsync(string cardId) => _refresh.ScheduleCardApply(cardId);

    public Task ApplyFiltersAsync() => ApplyFiltersAsync(CancellationToken.None);

    public Task ApplyFiltersAsync(CancellationToken cancellationToken) =>
        _refresh.RefreshDashboardAsync(cancellationToken);

    public Task ApplyCardFiltersAsync(string cardId) =>
        _refresh.RefreshCardLocalAsync(cardId);

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

    public string FiltersGridClass()
    {
        var chrome = _session.Document.FiltersChrome;
        if (chrome.IsBarLayout)
        {
            return chrome.IsStickyLine
                ? "filters-toolbar-row filters-toolbar-sticky"
                : "filters-toolbar-row";
        }

        return chrome.IsStickyLine
            ? "filters-grid filters-grid-sticky filters-grid-layout"
            : "filters-grid filters-grid-layout";
    }

    public string FiltersGridStyle() =>
        _session.Document.FiltersChrome.IsBarLayout
            ? string.Empty
            : DashboardLayoutHelper.CardsGridStyle(_session.Document.Layout);

    public void Dispose()
    {
        _refresh.StateChanged -= OnRefreshStateChanged;
        if (_reloadNotifier is not null)
        {
            _reloadNotifier.Changed -= OnDevSpecFileChanged;
        }

        _refresh.Dispose();
    }

    private void OnRefreshStateChanged() => Notify();

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
                if (!string.IsNullOrWhiteSpace(_session.ActiveCatalogEntryId))
                {
                    await _session.LoadCatalogEntryAsync(_session.ActiveCatalogEntryId).ConfigureAwait(false);
                }
                else
                {
                    await _session.LoadAsync(_session.CurrentSpecReference).ConfigureAwait(false);
                }
                LoadedSpecSource = _session.LoadedSpecSource;
                await InitializeDashboardStateAsync().ConfigureAwait(false);
                Error = null;
                Loaded = true;
                Notify();
                _ = RefreshCardsInBackgroundAsync();
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
        RecomputeToolbarPlacements();
        FiltersToCards = FilterBinding.MapFiltersToCards(_session.Document, _session.SpecLibrary)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        _refresh.FiltersToCards = FiltersToCards;

        _cardViewState.ClearAll();
        _filters.LoadFromSession(_session, PlacedFilterNames());
        SnapAllGrainAnchoredDates();
        _refresh.SeedAllCardSkeletons();

        await Task.CompletedTask;
    }

    private bool IsCardLocalManualApply(string cardId) =>
        _session.Document.Cards.Any(card =>
            string.Equals(card.Id, cardId, StringComparison.OrdinalIgnoreCase) &&
            card.LocalFiltersManualApply);

    private void NormalizeGrainDates(string filterName)
    {
        if (GrainFilterPresentation.IsGrainHostFilter(filterName, _session.FilterIndex))
        {
            GrainFilterPresentation.SnapAnchoredDates(
                filterName,
                _session.FilterIndex,
                SelectedFields,
                DateFrom,
                DateTo,
                ResolvePeriodReferenceDate());
        }
        else
        {
            GrainFilterPresentation.NormalizeAnchoredDates(
                filterName,
                _session.FilterIndex,
                SelectedFields,
                DateFrom,
                DateTo);
        }
    }

    private void SnapAllGrainAnchoredDates()
    {
        var reference = ResolvePeriodReferenceDate();
        foreach (var grainHost in _session.FilterIndex.Values
                     .Select(filter => filter.GrainFilterName)
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            GrainFilterPresentation.SnapAnchoredDates(
                grainHost!,
                _session.FilterIndex,
                SelectedFields,
                DateFrom,
                DateTo,
                reference);
        }
    }

    private DateOnly ResolvePeriodReferenceDate()
    {
        foreach (var filterName in _session.Document.DashboardFilters)
        {
            if (!_session.FilterIndex.TryGetValue(filterName, out var filter) ||
                filter.Kind is not FilterKind.Date ||
                filter.IsDayWidget)
            {
                continue;
            }

            if (DateTo.TryGetValue(filterName, out var to))
            {
                return to;
            }
        }

        return DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private void RecomputeToolbarPlacements()
    {
        var visible = VisibleToolbarFilterNames();
        var visibleSet = visible.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var placements = new Dictionary<string, PlacementDefinition>(
            ToolbarLayoutCompactor.CompactVisible(_session.Document, visibleSet),
            StringComparer.OrdinalIgnoreCase);

        var unplaced = visible.Where(name => !placements.ContainsKey(name)).ToList();
        if (unplaced.Count > 0)
        {
            var columns = _session.Document.Layout.Columns;
            var row = placements.Count == 0
                ? 1
                : placements.Values.Max(placement => placement.Row) + 1;
            var span = unplaced.Count == 1 ? columns : columns / unplaced.Count;
            for (var i = 0; i < unplaced.Count; i++)
            {
                placements[unplaced[i]] = new PlacementDefinition(row, 1 + i * span, span);
            }
        }

        ToolbarPlacements = placements;
    }

    private IReadOnlyList<string> VisibleToolbarFilterNames()
    {
        var all = _session.Document.DashboardFilters;
        if (_session.Document.Tabs.Count == 0 || string.IsNullOrWhiteSpace(ActiveTabId))
        {
            return all;
        }

        var tab = _session.Document.Tabs.FirstOrDefault(t =>
            string.Equals(t.Id, ActiveTabId, StringComparison.OrdinalIgnoreCase));
        if (tab is null)
        {
            return all;
        }

        var tabCardIds = tab.CardIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return all
            .Where(filterName =>
                FiltersToCards.TryGetValue(filterName, out var cards) &&
                cards.Any(cardId => tabCardIds.Contains(cardId)))
            .ToList();
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

    private IEnumerable<string> PlacedFilterNames() =>
        _session.Document.DashboardFilters
            .Concat(_session.Document.Cards.SelectMany(c => c.LocalFilters))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private void Notify() => Changed?.Invoke();
}
