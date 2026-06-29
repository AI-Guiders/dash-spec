using DashSpec.Core.Compilation;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using DashSpec.Host.Components;
using DashSpec.Host.Services;
using DashSpec.Host.Services.Models;
using DashSpec.Host.Services.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace DashSpec.Host.Components.Pages;

public partial class Home : IDisposable
{
    [Inject] private DashboardSessionService Dashboard { get; set; } = default!;

    private bool _loaded;
    private bool _busy;
    private bool _switching;
    private string? _error;
    private string? _loadedSpecSource;
    private List<CardRenderResult> _cards = [];
    private Dictionary<string, DateOnly> _dateFrom = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, DateOnly> _dateTo = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, HashSet<string>> _selectedFields = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, int> _topLimits = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, IReadOnlyList<string>> _filtersToCards = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, PlacementDefinition> _tabPlacements = new(StringComparer.OrdinalIgnoreCase);
    private string? _activeTabId;
    private CancellationTokenSource? _dashboardApplyCts;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await Dashboard.LoadAsync();
            _loadedSpecSource = Dashboard.LoadedSpecSource;
            await InitializeDashboardStateAsync();
            _loaded = true;
            await RefreshCardsAsync();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
    }

    private async Task OnSpecUploadedAsync(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file is null)
        {
            return;
        }

        _switching = true;
        _loaded = false;
        _error = null;
        _dashboardApplyCts?.Cancel();
        StateHasChanged();

        try
        {
            const long maxBytes = 2 * 1024 * 1024;
            await using var stream = file.OpenReadStream(maxBytes);
            await Dashboard.LoadFromUploadAsync(stream, file.Name);
            _loadedSpecSource = Dashboard.LoadedSpecSource;
            await InitializeDashboardStateAsync();
            _loaded = true;
            await RefreshCardsAsync();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
            _loaded = false;
        }
        finally
        {
            _switching = false;
        }
    }

    private async Task InitializeDashboardStateAsync()
    {
        _activeTabId = Dashboard.Document.Tabs.FirstOrDefault()?.Id;
        RecomputeTabPlacements();
        _filtersToCards = FilterBinding.MapFiltersToCards(Dashboard.Document, Dashboard.SpecLibrary)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

        _dateFrom.Clear();
        _dateTo.Clear();
        _selectedFields.Clear();
        _topLimits.Clear();

        foreach (var filterName in PlacedFilterNames())
        {
            var filter = Dashboard.FilterIndex[filterName];
            if (filter.Kind is FilterKind.Date)
            {
                var range = Dashboard.Filters.GetDate(filter.Name)
                    ?? DateDefaultRange.Resolve(filter.DefaultExpression!, DateOnly.FromDateTime(DateTime.UtcNow));
                _dateFrom[filter.Name] = range.From;
                _dateTo[filter.Name] = range.To;
            }
            else if (filter.Kind is FilterKind.Field)
            {
                _selectedFields[filter.Name] = [];
            }
            else if (filter.Kind is FilterKind.Top)
            {
                _topLimits[filter.Name] = TopLimitDefaults.Resolve(
                    filter,
                    Dashboard.Filters.GetTop(filter.Name));
            }
        }

        await Task.CompletedTask;
    }

    private void SelectTab(string tabId)
    {
        _activeTabId = tabId;
        RecomputeTabPlacements();
        StateHasChanged();
    }

    private void RecomputeTabPlacements()
    {
        _tabPlacements.Clear();
        if (Dashboard.Document.Tabs.Count == 0 || string.IsNullOrWhiteSpace(_activeTabId))
        {
            return;
        }

        foreach (var (title, placement) in TabLayoutCompactor.Compact(Dashboard.Document, _activeTabId, Dashboard.SpecLibrary))
        {
            _tabPlacements[title] = placement;
        }
    }

    private IEnumerable<CardRenderResult> VisibleCards()
    {
        if (Dashboard.Document.Tabs.Count == 0 || string.IsNullOrWhiteSpace(_activeTabId))
        {
            return _cards;
        }

        var tab = Dashboard.Document.Tabs.Single(t =>
            string.Equals(t.Id, _activeTabId, StringComparison.OrdinalIgnoreCase));

        return _cards.Where(card =>
            tab.CardIds.Contains(card.Id, StringComparer.OrdinalIgnoreCase));
    }

    private void OnDashboardFieldChanged((string Name, HashSet<string> Values) args)
    {
        _selectedFields[args.Name] = args.Values;
        ScheduleDashboardApplyAsync();
    }

    private void OnDashboardDayChanged(string name)
    {
        if (_dateFrom.TryGetValue(name, out var day))
        {
            _dateTo[name] = day;
        }

        ScheduleDashboardApplyAsync();
    }

    private void OnCardDayChanged((string CardId, string FilterName) args)
    {
        if (_dateFrom.TryGetValue(args.FilterName, out var day))
        {
            _dateTo[args.FilterName] = day;
        }

        ScheduleCardApplyAsync(args.CardId);
    }

    private void OnCardFieldChanged((string CardId, string FilterName, HashSet<string> Values) args)
    {
        _selectedFields[args.FilterName] = args.Values;
        ScheduleCardApplyAsync(args.CardId);
    }

    private void ScheduleDashboardApplyAsync()
    {
        if (!Dashboard.Document.FiltersChrome.IsAutoApply)
        {
            return;
        }

        _dashboardApplyCts?.Cancel();
        _dashboardApplyCts?.Dispose();
        _dashboardApplyCts = new CancellationTokenSource();
        var token = _dashboardApplyCts.Token;
        _ = DebouncedDashboardApplyAsync(token);
    }

    private async Task DebouncedDashboardApplyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Dashboard.Document.FiltersChrome.DebounceMs, cancellationToken);
            await ApplyFiltersAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ScheduleCardApplyAsync(string cardId)
    {
        if (!Dashboard.Document.FiltersChrome.IsAutoApply)
        {
            return;
        }

        _ = ApplyCardFiltersAsync(cardId);
    }

    private async Task ApplyFiltersAsync()
    {
        foreach (var filterName in Dashboard.Document.DashboardFilters)
        {
            var filter = Dashboard.FilterIndex[filterName];
            if (filter.Kind is FilterKind.Date &&
                _dateFrom.TryGetValue(filter.Name, out var from) &&
                _dateTo.TryGetValue(filter.Name, out var to))
            {
                Dashboard.ApplyDateFilter(filter.Name, from, to);
            }
            else if (filter.Kind is FilterKind.Field &&
                     _selectedFields.TryGetValue(filter.Name, out var selected))
            {
                Dashboard.ApplyFieldFilter(filter.Name, selected);
            }
        }

        await RefreshCardsAsync();
    }

    private async Task ApplyCardFiltersAsync(string cardId)
    {
        var card = Dashboard.Document.Cards.Single(c =>
            string.Equals(c.Id, cardId, StringComparison.OrdinalIgnoreCase));

        foreach (var filterName in card.LocalFilters)
        {
            var filter = Dashboard.FilterIndex[filterName];
            if (filter.Kind is FilterKind.Date &&
                _dateFrom.TryGetValue(filter.Name, out var from) &&
                _dateTo.TryGetValue(filter.Name, out var to))
            {
                Dashboard.ApplyDateFilter(filter.Name, from, to);
            }
            else if (filter.Kind is FilterKind.Field &&
                     _selectedFields.TryGetValue(filter.Name, out var selected))
            {
                Dashboard.ApplyFieldFilter(filter.Name, selected);
            }
            else if (filter.Kind is FilterKind.Top &&
                     _topLimits.TryGetValue(filter.Name, out var topLimit))
            {
                Dashboard.ApplyTopFilter(filter.Name, TopLimitDefaults.Resolve(filter, topLimit));
            }
        }

        var index = Dashboard.Document.Cards
            .Select((definition, i) => (definition, i))
            .Single(x => string.Equals(x.definition.Id, cardId, StringComparison.OrdinalIgnoreCase))
            .i;

        try
        {
            var result = await Dashboard.RenderCardAsync(card);
            _cards[index] = result;
        }
        catch (Exception ex)
        {
            _cards[index] = _cards[index] with { Error = ex.Message, Loading = false };
        }

        StateHasChanged();
    }

    private IEnumerable<string> PlacedFilterNames() =>
        Dashboard.Document.DashboardFilters
            .Concat(Dashboard.Document.Cards.SelectMany(c => c.LocalFilters))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    public void Dispose()
    {
        _dashboardApplyCts?.Cancel();
        _dashboardApplyCts?.Dispose();
    }

    private async Task RefreshCardsAsync()
    {
        _busy = true;
        _cards = Dashboard.Document.Cards
            .Select(card =>
            {
                var kind = DiagramKindRegistry.Resolve(card.Diagram.Kind);
                return new CardRenderResult(
                    card.Id,
                    card.Title,
                    card.Diagram.Kind,
                    kind.DataFamily,
                    Loading: true,
                    BoundFilters: card.BoundFilters,
                    LocalFilters: card.LocalFilters,
                    Placement: card.Placement,
                    ChartPresentation: kind.DataFamily is DiagramDataFamily.Chart
                        ? CardChromeResolver.ResolveChartPresentation(card, Dashboard.SpecLibrary)
                        : null,
                    MatrixPresentation: kind.DataFamily is DiagramDataFamily.Matrix
                        ? MatrixPresentation.FromCard(card, Dashboard.SpecLibrary)
                        : null);
            })
            .ToList();
        StateHasChanged();

        var cardDefs = Dashboard.Document.Cards.ToList();
        var results = new CardRenderResult[cardDefs.Count];

        await Task.WhenAll(cardDefs.Select(async (card, index) =>
        {
            try
            {
                results[index] = await Dashboard.RenderCardAsync(card);
            }
            catch (Exception ex)
            {
                var kind = DiagramKindRegistry.Resolve(card.Diagram.Kind);
                results[index] = new CardRenderResult(
                    card.Id,
                    card.Title,
                    card.Diagram.Kind,
                    kind.DataFamily,
                    Error: ex.Message,
                    BoundFilters: card.BoundFilters,
                    LocalFilters: card.LocalFilters,
                    Placement: card.Placement,
                    ChartPresentation: kind.DataFamily is DiagramDataFamily.Chart
                        ? CardChromeResolver.ResolveChartPresentation(card, Dashboard.SpecLibrary)
                        : null,
                    MatrixPresentation: kind.DataFamily is DiagramDataFamily.Matrix
                        ? MatrixPresentation.FromCard(card, Dashboard.SpecLibrary)
                        : null);
            }
        }));

        await InvokeAsync(() =>
        {
            _cards = results.ToList();
            _busy = false;
            StateHasChanged();
        });
    }

    private IReadOnlyList<string> DashboardBoundForCard(CardRenderResult card) =>
        (card.BoundFilters ?? [])
            .Where(name => Dashboard.Document.DashboardFilters.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToList();

    private string FiltersSectionClass()
    {
        var chrome = Dashboard.Document.FiltersChrome;
        var filtersClass = chrome.IsBarLayout ? "filters filters-bar" : "filters filters-card";
        if (chrome.IsStickyCard)
        {
            filtersClass += " filters-sticky-card";
        }

        return filtersClass;
    }

    private string FiltersGridClass() =>
        Dashboard.Document.FiltersChrome.IsStickyLine
            ? "filters-grid filters-grid-sticky"
            : "filters-grid";
}
