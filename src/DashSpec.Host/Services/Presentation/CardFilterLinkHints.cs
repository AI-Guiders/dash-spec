using DashSpec.Core.Model;

namespace DashSpec.Host.Services.Presentation;

internal static class CardFilterLinkHints
{
    public static (string? Hint, string? CssClass) Resolve(CardDefinition card, DashboardDocument document)
    {
        const string topFilter = "chart_top";

        if (card.LocalFilters.Contains(topFilter, StringComparer.OrdinalIgnoreCase))
        {
            var sameListGuests = document.Cards
                .Where(IsSameListGuest(card, topFilter))
                .Select(c => c.Title)
                .ToList();

            if (sameListGuests.Count > 0)
            {
                return (
                    $"Топ N — общий рейтинг с «{sameListGuests[0]}»",
                    "card-link-group-host");
            }
        }

        if (string.IsNullOrWhiteSpace(card.FilterHostCardId) ||
            card.HostedFilters is null ||
            !card.HostedFilters.Contains(topFilter, StringComparer.OrdinalIgnoreCase))
        {
            return (null, null);
        }

        var host = document.Cards.FirstOrDefault(c =>
            string.Equals(c.Id, card.FilterHostCardId, StringComparison.OrdinalIgnoreCase));
        if (host is null)
        {
            return (null, null);
        }

        if (string.Equals(card.DataSource.Value, host.DataSource.Value, StringComparison.OrdinalIgnoreCase))
        {
            return ($"Тот же рейтинг, что в «{host.Title}»", "card-link-group-guest");
        }

        return ($"Тот же N, что в «{host.Title}»", "card-link-n-shared");
    }

    private static Func<CardDefinition, bool> IsSameListGuest(CardDefinition host, string topFilter) =>
        guest =>
            string.Equals(guest.FilterHostCardId, host.Id, StringComparison.OrdinalIgnoreCase) &&
            guest.HostedFilters is not null &&
            guest.HostedFilters.Contains(topFilter, StringComparer.OrdinalIgnoreCase) &&
            string.Equals(guest.DataSource.Value, host.DataSource.Value, StringComparison.OrdinalIgnoreCase);
}
