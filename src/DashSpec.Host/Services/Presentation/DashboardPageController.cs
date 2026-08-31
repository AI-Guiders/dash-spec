using DashSpec.Abstractions.Plugins;
using DashSpec.Core.Analysis;
using DashSpec.Core.Compilation;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Runtime;
using DashSpec.Host.Configuration;
using DashSpec.Host.Plugins;
using DashSpec.Host.Plugins.Builtins;
using DashSpec.Host.Commands;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Dev;
using DashSpec.Host.Services.Diagnostics;
using DashSpec.Host.Services.Loading;
using DashSpec.Host.Services.Models;
using DashSpec.Host.Services.Presentation;
using DashSpec.Host.Services.Rendering;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;

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
    private readonly DashboardFilterCommandService _filterCommands;
    private readonly IDashboardCultureAmbient _cultureAmbient;
    private readonly DevSpecReloadNotifier? _reloadNotifier;
    private readonly LoadTrace _loadTrace;
    private readonly ILogger<DashboardPageController> _logger;
    private readonly NavigationManager _navigation;

    public DashboardPageController(
        IDashboardSession session,
        OnClickInteractionService interactions,
        DashSpecActionDispatcher actions,
        ICardViewState cardViewState,
        DashSpecHostContext hostContext,
        DashboardFilterUiState filters,
        DashboardRefreshCoordinator refresh,
        DashboardFilterCommandService filterCommands,
        IDashboardCultureAmbient cultureAmbient,
        IWebHostEnvironment environment,
        DevSpecReloadNotifier reloadNotifier,
        LoadTrace loadTrace,
        ILogger<DashboardPageController> logger,
        NavigationManager navigation)
    {
        _session = session;
        _interactions = interactions;
        _actions = actions;
        _cardViewState = cardViewState;
        _hostContext = hostContext;
        _filters = filters;
        _refresh = refresh;
        _filterCommands = filterCommands;
        _cultureAmbient = cultureAmbient;
        _loadTrace = loadTrace;
        _logger = logger;
        _navigation = navigation;
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
    public string? CommandError { get; private set; }
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

    public string ActivePhaseId { get; private set; } = "browse";

    public string? ActivePageId { get; private set; }

    public bool HasCatalog => CatalogEntries.Count > 1;

    public IReadOnlyList<CatalogGroupDefinition> CatalogGroups =>
        _hostContext.Catalog.Document.Groups ?? [];

    public bool HasPages => ActiveTabPages().Count > 1;

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
        ResetActivePageForTab();
        RecomputeTabPlacements();
        RecomputeToolbarPlacements();
        Notify();
        await Task.CompletedTask;
    }

    public async Task SelectPageAsync(string pageId)
    {
        if (string.Equals(ActivePageId, pageId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ActivePageId = pageId;
        ActivePhaseId = "browse";
        _refresh.ActivePhaseId = ActivePhaseId;
        SyncUsageDateFromActivePage();
        RecomputeTabPlacements();
        RecomputeToolbarPlacements();
        Notify();
        await ApplyFiltersAsync().ConfigureAwait(false);
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

        var effects = _interactions.ExpandClickEffects(card.ClickBehaviour.Effects).ToList();
        // Stacked field filters from sequential chart clicks often yield empty detail cards
        // (e.g. location=/PROJECTHUB AND program=DESIGN). Keep date; replace sibling fields.
        var fieldFiltersThisClick = effects
            .OfType<SetFilterFromFieldEffect>()
            .Select(x => x.FilterName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (fieldFiltersThisClick.Count > 0)
        {
            foreach (var name in SelectedFields.Keys.ToList())
            {
                if (!fieldFiltersThisClick.Contains(name))
                {
                    SelectedFields.Remove(name);
                }
            }
        }

        var navigate = false;
        GotoCatalogEntryEffect? catalogGoto = null;
        var clickSetFilters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var effect in effects)
        {
            if (effect is GotoCatalogEntryEffect gotoEntry)
            {
                catalogGoto = gotoEntry;
                continue;
            }

            switch (effect)
            {
                case SetFilterFromFieldEffect setEffect:
                    ApplyFilterFromHeatmapCell(setEffect, context);
                    clickSetFilters.Add(setEffect.FilterName);
                    navigate = true;
                    break;
                case GotoTabEffect gotoEffect:
                    ActiveTabId = gotoEffect.TabId;
                    RecomputeTabPlacements();
                    RecomputeToolbarPlacements();
                    navigate = true;
                    break;
                case FocusPhaseEffect focusEffect:
                    ActivePhaseId = focusEffect.PhaseId;
                    _refresh.ActivePhaseId = ActivePhaseId;
                    navigate = true;
                    break;
                case GotoPageEffect gotoPage:
                    ActivePageId = gotoPage.PageId;
                    ActivePhaseId = "browse";
                    _refresh.ActivePhaseId = ActivePhaseId;
                    RecomputeTabPlacements();
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

        if (catalogGoto is not null)
        {
            var carriedFilters = BuildCarriedFiltersForCatalogEntry(catalogGoto, clickSetFilters);
            await SelectCatalogEntryAsync(catalogGoto.EntryId, carriedFilters, cancellationToken).ConfigureAwait(false);
            return;
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

        // Folded "Other"/"Прочие" is not a real dimension value — filtering by it yields empty cards.
        if (string.Equals(raw, "Other", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "Прочие", StringComparison.OrdinalIgnoreCase))
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

    private FilterUiSnapshot? BuildCarriedFiltersForCatalogEntry(
        GotoCatalogEntryEffect gotoEntry,
        IReadOnlySet<string> clickSetFilters)
    {
        var snapshot = _filters.Capture();
        if (gotoEntry.PreserveFilterNames is null)
        {
            return clickSetFilters.Count == 0
                ? null
                : snapshot.NarrowTo(clickSetFilters);
        }

        if (gotoEntry.PreserveFilterNames.Count == 0)
        {
            return snapshot;
        }

        var names = clickSetFilters
            .Concat(gotoEntry.PreserveFilterNames)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return snapshot.NarrowTo(names);
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
        await SelectCatalogEntryAsync(entryId, carriedFilters: null, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task SelectCatalogEntryAsync(
        string entryId,
        FilterUiSnapshot? carriedFilters,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(_session.ActiveCatalogEntryId, entryId, StringComparison.OrdinalIgnoreCase))
        {
            if (carriedFilters is null)
            {
                return;
            }

            _filters.ApplySnapshot(carriedFilters, _session.FilterIndex);
            _filters.SyncToSession(_session, PlacedFilterNames());
            Notify();
            await ApplyFiltersAsync(cancellationToken).ConfigureAwait(false);
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
            if (carriedFilters is not null)
            {
                _filters.ApplySnapshot(carriedFilters, _session.FilterIndex);
                _filters.SyncToSession(_session, PlacedFilterNames());
            }
            Loaded = true;
            Notify();
            _ = RefreshCardsInBackgroundAsync();
            if (carriedFilters is not null)
            {
                await ApplyFiltersAsync(cancellationToken).ConfigureAwait(false);
            }
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
        IEnumerable<CardRenderResult> cards;
        if (_session.Document.Tabs.Count == 0 || string.IsNullOrWhiteSpace(ActiveTabId))
        {
            cards = _refresh.Cards;
        }
        else
        {
            var tab = _session.Document.Tabs.Single(t =>
                string.Equals(t.Id, ActiveTabId, StringComparison.OrdinalIgnoreCase));

            cards = _refresh.Cards.Where(card =>
                tab.CardIds.Contains(card.Id, StringComparer.OrdinalIgnoreCase));
        }

        return cards.Where(card =>
        {
            var definition = _session.Document.Cards.FirstOrDefault(c =>
                string.Equals(c.Id, card.Id, StringComparison.OrdinalIgnoreCase));
            if (definition is null)
            {
                return true;
            }

            return CardVisibilityEvaluator.Evaluate(definition, SelectedFields, ActivePhaseId)
                is CardVisibilityOutcome.Visible or CardVisibilityOutcome.Placeholder;
        })
        .Where(card =>
        {
            if (string.IsNullOrWhiteSpace(ActivePageId))
            {
                return true;
            }

            var definition = FindCardDefinition(card.Id);
            return definition is null ||
                   string.Equals(definition.PageId, ActivePageId, StringComparison.OrdinalIgnoreCase);
        });
    }

    public IReadOnlyList<ReportPageDefinition> ActiveTabPages() => ResolveActiveTabPages();

    public CardDefinition? FindCardDefinition(string cardId) =>
        _session.Document.Cards.FirstOrDefault(c =>
            string.Equals(c.Id, cardId, StringComparison.OrdinalIgnoreCase));

    public void OnDashboardFieldChanged((string Name, HashSet<string> Values) args)
    {
        SelectedFields[args.Name] = args.Values;
        if (string.Equals(args.Name, "user_name", StringComparison.OrdinalIgnoreCase))
        {
            ActivePhaseId = CardVisibilityEvaluator.FilterHasSelection(args.Name, SelectedFields)
                ? "detail"
                : "browse";
            _refresh.ActivePhaseId = ActivePhaseId;
        }

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

    public async Task OnFilterCommandCommittedAsync(string line)
    {
        CommandError = null;
        var run = _filterCommands.TryExecute(line, BuildCommandContext());
        if (!run.Outcome.Success)
        {
            _logger.LogWarning("Filter command failed: {Error}", run.Outcome.Error);
            CommandError = run.Outcome.Error;
            Notify();
            return;
        }

        if (run.PendingCatalogEntryId is not null)
        {
            await SelectCatalogEntryAsync(run.PendingCatalogEntryId).ConfigureAwait(false);
            return;
        }

        if (run.PendingPageId is not null)
        {
            await SelectPageAsync(run.PendingPageId).ConfigureAwait(false);
            return;
        }

        if (run.PendingHostRoute is not null)
        {
            _navigation.NavigateTo(run.PendingHostRoute);
            return;
        }

        if (run.PendingCardId is not null && run.PendingViewId is not null)
        {
            _cardViewState.SetActiveView(run.PendingCardId, run.PendingViewId);
            CommandError = null;
            Notify();
            await _refresh.RefreshSingleCardAsync(run.PendingCardId, CancellationToken.None).ConfigureAwait(false);
            return;
        }

        _filters.SyncToSession(_session, PlacedFilterNames());
        SyncUsageDateFromActivePage();
        CommandError = null;
        Notify();
        await ApplyFiltersAsync().ConfigureAwait(false);
    }

    public DashboardFilterContext BuildCommandContext() =>
        new()
        {
            ReportId = _session.Document.Id,
            ActiveScope = [DashSpecCommandScope.Dashboard],
            FilterIndex = _session.FilterIndex,
            ToolbarFilterNames = VisibleToolbarFilterNames(),
            CommandAliases = _session.Document.ResolvedCommandAliases,
            UiState = _filters,
            GetFieldOptions = _session.GetFieldOptions,
            CatalogEntries = CatalogEntries,
            ReportPages = ActiveTabPages(),
            ActiveCatalogEntryId = _session.ActiveCatalogEntryId,
            ActivePageId = ActivePageId,
            SwitchableCards = BuildSwitchableCards(),
            Culture = _cultureAmbient.Culture,
        };

    IReadOnlyList<DashboardCardCommandTarget> BuildSwitchableCards() =>
        DashboardCardCommandTargetsBuilder.Build(
            VisibleCards()
                .Select(card => FindCardDefinition(card.Id))
                .Where(definition => definition is not null)
                .Cast<CardDefinition>());

    public Task ApplyFiltersAsync() => ApplyFiltersAsync(CancellationToken.None);

    public Task ApplyFiltersAsync(CancellationToken cancellationToken)
    {
        _refresh.ActivePhaseId = ActivePhaseId;
        return _refresh.RefreshDashboardAsync(cancellationToken);
    }

    public Task ApplyCardFiltersAsync(string cardId) =>
        _refresh.RefreshCardLocalAsync(cardId);

    public IReadOnlyList<string> DashboardBoundForCard(CardRenderResult card)
    {
        var definition = FindCardDefinition(card.Id);
        if (definition?.Chrome?.BoundFilters is CardBoundFilterChrome.Hidden or CardBoundFilterChrome.ToolbarOnly)
        {
            return [];
        }

        var visibleToolbar = VisibleToolbarFilterNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
        return (card.BoundFilters ?? [])
            .Where(name => _session.Document.DashboardFilters.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Where(name => !visibleToolbar.Contains(name))
            .ToList();
    }

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
        ActivePhaseId = "browse";
        _refresh.ActivePhaseId = ActivePhaseId;
        ResetActivePageForTab();
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
        var pageToolbar = PageToolbarResolver.ResolveActiveToolbarBoard(
            _session.Document,
            ActiveTabId,
            ActivePageId);

        var visible = ToolbarFilterVisibility.ResolveVisibleFilters(
            _session.Document,
            ActiveTabId,
            ActivePageId,
            FiltersToCards);

        if (pageToolbar is not null)
        {
            var allowed = pageToolbar.Rows
                .SelectMany(row => row)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            visible = visible
                .Where(name => allowed.Contains(name))
                .OrderBy(name =>
                {
                    var index = 0;
                    foreach (var row in pageToolbar.Rows)
                    {
                        foreach (var token in row)
                        {
                            if (string.Equals(token, name, StringComparison.OrdinalIgnoreCase))
                            {
                                return index;
                            }

                            index++;
                        }
                    }

                    return int.MaxValue;
                })
                .ToList();
        }

        var derive = PageToolbarResolver.ResolveUsageDateDerive(
            _session.Document,
            ActiveTabId,
            ActivePageId);
        return DeriveToolbarExpander.Expand(visible, derive, _session.FilterIndex);
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
                     _session.SpecLibrary,
                     ActivePageId))
        {
            TabPlacements[title] = placement;
        }
    }

    private void ResetActivePageForTab()
    {
        var pages = ResolveActiveTabPages();
        ActivePageId = pages.FirstOrDefault()?.Id;
        SyncUsageDateFromActivePage();
    }

    private void SyncUsageDateFromActivePage()
    {
        var derive = PageToolbarResolver.ResolveUsageDateDerive(
            _session.Document,
            ActiveTabId,
            ActivePageId);
        if (derive is null)
        {
            return;
        }

        if (!DateFrom.TryGetValue(derive.SourceFilter, out var anchor))
        {
            return;
        }

        var grain = PeriodAnchorResolver.TryReadGrain(_session.Filters, derive.GrainFilterName);
        var from = PeriodAnchorResolver.ResolveAnchor(anchor, grain);
        var to = PeriodAnchorResolver.ResolvePeriodEnd(from, grain);
        DateFrom[derive.TargetFilter] = from;
        DateTo[derive.TargetFilter] = to;
        _session.Filters.SetDate(derive.TargetFilter, from, to);
    }

    private IReadOnlyList<ReportPageDefinition> ResolveActiveTabPages()
    {
        if ((_session.Document.Pages ?? []).Count == 0)
        {
            return [];
        }

        var tabCardIds = ResolveActiveTabCardIds();
        if (tabCardIds.Count == 0)
        {
            return _session.Document.Pages ?? [];
        }

        var pageIds = _session.Document.Cards
            .Where(card => tabCardIds.Contains(card.Id) && !string.IsNullOrWhiteSpace(card.PageId))
            .Select(card => card.PageId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return (_session.Document.Pages ?? [])
            .Where(page =>
                (string.IsNullOrWhiteSpace(ActiveTabId) ||
                 PageTabScope.PageBelongsToTab(page, ActiveTabId)) &&
                pageIds.Contains(page.Id))
            .ToList();
    }

    private HashSet<string> ResolveActiveTabCardIds()
    {
        if (_session.Document.Tabs.Count == 0 || string.IsNullOrWhiteSpace(ActiveTabId))
        {
            return _session.Document.Cards
                .Select(card => card.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var tab = _session.Document.Tabs.FirstOrDefault(t =>
            string.Equals(t.Id, ActiveTabId, StringComparison.OrdinalIgnoreCase));
        return tab is null
            ? []
            : tab.CardIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private IEnumerable<string> PlacedFilterNames() =>
        PlacedFilterCollector.Collect(_session.Document);

    private void Notify() => Changed?.Invoke();
}
