using DashSpec.Core.Model;
using DashSpec.Host.Plugins;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Models;
using DashSpec.Host.Services.Rendering;

namespace DashSpec.Host.Services.Presentation;

/// <summary>Centralized card refresh pipeline: debounce, cancellation, partial updates.</summary>
public sealed class DashboardRefreshCoordinator : IDisposable
{
    private readonly IDashboardSession _session;
    private readonly VizPluginRegistry _vizPlugins;
    private readonly DashboardFilterUiState _filters;
    private CancellationTokenSource? _dashboardApplyCts;
    private CancellationTokenSource? _cardApplyCts;
    private CancellationTokenSource? _refreshCts;
    private long _refreshGeneration;

    public DashboardRefreshCoordinator(
        IDashboardSession session,
        VizPluginRegistry vizPlugins,
        DashboardFilterUiState filters)
    {
        _session = session;
        _vizPlugins = vizPlugins;
        _filters = filters;
    }

    public event Action? StateChanged;

    public Func<Func<Task>, Task>? UiDispatcher { get; set; }

    public bool Busy { get; private set; }

    public List<CardRenderResult> Cards { get; } = [];

    public IReadOnlyDictionary<string, IReadOnlyList<string>> FiltersToCards { get; set; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    public void SeedAllCardSkeletons()
    {
        var dashboardFilters = _session.Document.DashboardFilters;
        Cards.Clear();
        Cards.AddRange(_session.Document.Cards.Select(card => CardRenderSkeletonFactory.CreateLoading(
            card,
            _session.SpecLibrary,
            _vizPlugins,
            dashboardFilters,
            _session.Document)));
    }

    public void ScheduleDashboardApply()
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

    public void ScheduleCardApply(string cardId)
    {
        if (!_session.Document.FiltersChrome.IsAutoApply)
        {
            return;
        }

        _cardApplyCts?.Cancel();
        _cardApplyCts?.Dispose();
        _cardApplyCts = new CancellationTokenSource();
        var token = _cardApplyCts.Token;
        _ = DebouncedCardApplyAsync(cardId, token);
    }

    public Task RefreshAllAsync(CancellationToken cancellationToken = default) =>
        RefreshCardsAsync(cardIds: null, cancellationToken);

    public Task RefreshDashboardAsync(CancellationToken cancellationToken = default)
    {
        var cardIds = ResolveCardsForDashboardRefresh();
        return RefreshCardsAsync(cardIds, cancellationToken);
    }

    public Task RefreshCardLocalAsync(string cardId, CancellationToken cancellationToken = default) =>
        RefreshCardsAsync(CollectCardsForLocalFilterApply(cardId), cancellationToken);

    public Task RefreshSingleCardAsync(string cardId, CancellationToken cancellationToken = default) =>
        RefreshCardsAsync([cardId], cancellationToken);

    public void CancelPendingApplies()
    {
        _dashboardApplyCts?.Cancel();
        _cardApplyCts?.Cancel();
    }

    public void Dispose()
    {
        _dashboardApplyCts?.Cancel();
        _dashboardApplyCts?.Dispose();
        _cardApplyCts?.Cancel();
        _cardApplyCts?.Dispose();
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
    }

    private async Task DebouncedDashboardApplyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_session.Document.FiltersChrome.DebounceMs, cancellationToken).ConfigureAwait(false);
            await RefreshDashboardAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task DebouncedCardApplyAsync(string cardId, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_session.Document.FiltersChrome.DebounceMs, cancellationToken).ConfigureAwait(false);
            await RefreshCardLocalAsync(cardId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RefreshCardsAsync(
        IReadOnlyList<string>? cardIds,
        CancellationToken cancellationToken)
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = new CancellationTokenSource();
        _refreshGeneration++;
        var generation = _refreshGeneration;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _refreshCts.Token);
        var token = linked.Token;

        try
        {
            _filters.SyncToSession(_session, PlacedFilterNames());
            var targetIds = ResolveTargetCardIds(cardIds);
            await SetCardsLoadingAsync(targetIds).ConfigureAwait(false);

            var dashboardFilters = _session.Document.DashboardFilters;
            var cardDefs = _session.Document.Cards
                .Where(card => targetIds.Contains(card.Id))
                .ToList();

            var rendered = await Task.WhenAll(cardDefs.Select(async card =>
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var result = await _session.RenderCardAsync(card, token).ConfigureAwait(false);
                    return (card.Id, result);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    return (card.Id, CardRenderSkeletonFactory.CreateError(
                        card,
                        _session.SpecLibrary,
                        _vizPlugins,
                        dashboardFilters,
                        ex.Message,
                        _session.Document));
                }
            })).ConfigureAwait(false);

            if (!IsCurrentRefresh(generation))
            {
                return;
            }

            await MergeCardResultsAsync(rendered, generation).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!IsCurrentRefresh(generation))
        {
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (IsCurrentRefresh(generation))
            {
                await SetBusyFalseAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task SetCardsLoadingAsync(IReadOnlySet<string> cardIds)
    {
        var dashboardFilters = _session.Document.DashboardFilters;
        await DispatchUiAsync(() =>
        {
            Busy = true;
            if (Cards.Count != _session.Document.Cards.Count)
            {
                SeedAllCardSkeletons();
            }
            else
            {
                for (var index = 0; index < Cards.Count; index++)
                {
                    if (!cardIds.Contains(Cards[index].Id))
                    {
                        continue;
                    }

                    var card = _session.Document.Cards[index];
                    Cards[index] = CardRenderSkeletonFactory.CreateLoading(
                        card,
                        _session.SpecLibrary,
                        _vizPlugins,
                        dashboardFilters,
                        _session.Document);
                }
            }

            NotifyStateChanged();
            return Task.CompletedTask;
        }).ConfigureAwait(false);
    }

    private async Task MergeCardResultsAsync(
        (string Id, CardRenderResult Result)[] rendered,
        long generation)
    {
        if (!IsCurrentRefresh(generation))
        {
            return;
        }

        await DispatchUiAsync(() =>
        {
            foreach (var (id, result) in rendered)
            {
                var index = Cards.FindIndex(card =>
                    string.Equals(card.Id, id, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    Cards[index] = result;
                }
            }

            Busy = false;
            NotifyStateChanged();
            return Task.CompletedTask;
        }).ConfigureAwait(false);
    }

    private async Task SetBusyFalseAsync()
    {
        await DispatchUiAsync(() =>
        {
            Busy = false;
            NotifyStateChanged();
            return Task.CompletedTask;
        }).ConfigureAwait(false);
    }

    private bool IsCurrentRefresh(long generation) => generation == _refreshGeneration;

    private HashSet<string> ResolveTargetCardIds(IReadOnlyList<string>? cardIds)
    {
        if (cardIds is { Count: > 0 })
        {
            return cardIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        return _session.Document.Cards
            .Select(card => card.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private IReadOnlyList<string> ResolveCardsForDashboardRefresh()
    {
        var cardIds = _session.Document.DashboardFilters
            .Where(FiltersToCards.ContainsKey)
            .SelectMany(filterName => FiltersToCards[filterName])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return cardIds.Count > 0
            ? cardIds
            : _session.Document.Cards.Select(card => card.Id).ToList();
    }

    private IReadOnlyList<string> CollectCardsForLocalFilterApply(string cardId)
    {
        var cardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { cardId };
        var card = _session.Document.Cards.Single(c =>
            string.Equals(c.Id, cardId, StringComparison.OrdinalIgnoreCase));

        foreach (var filterName in card.LocalFilters)
        {
            foreach (var boundCard in _session.Document.Cards)
            {
                if (boundCard.BoundFilters.Contains(filterName, StringComparer.OrdinalIgnoreCase))
                {
                    cardIds.Add(boundCard.Id);
                }
            }
        }

        foreach (var dependent in _session.Document.Cards)
        {
            if (string.Equals(dependent.FilterHostCardId, cardId, StringComparison.OrdinalIgnoreCase))
            {
                cardIds.Add(dependent.Id);
            }
        }

        return cardIds.ToList();
    }

    private IEnumerable<string> PlacedFilterNames() =>
        _session.Document.DashboardFilters
            .Concat(_session.Document.Cards.SelectMany(c => c.LocalFilters))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private async Task DispatchUiAsync(Func<Task> action)
    {
        if (UiDispatcher is not null)
        {
            await UiDispatcher(action).ConfigureAwait(false);
        }
        else
        {
            await action().ConfigureAwait(false);
        }
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}
