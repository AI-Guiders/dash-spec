#nullable enable

namespace DashSpec.Host.Commands;

internal static class DashboardCommandLineNormalizer
{
    public static string NormalizeExecutableLine(string line, DashboardFilterContext context)
    {
        var body = DashboardFilterSlashCompletion.SanitizeLine(line).TrimStart();
        if (body.Length == 0)
        {
            return "";
        }

        var tokens = body.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return "";
        }

        if (tokens[0].Equals(FilterCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeSelect(tokens, context);
        }

        if (tokens[0].Equals(DashCatalog.CardViewPhrase.LiteralPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeView(tokens, context);
        }

        if (StartsWithPhrasePrefix(body, DashCatalog.MatrixValuesTogglePhrase.LiteralPrefix))
        {
            return NormalizeMatrixToggle(body, context, DashCatalog.MatrixValuesTogglePhrase.LiteralPrefix);
        }

        if (StartsWithPhrasePrefix(body, DashCatalog.MatrixAxisXTogglePhrase.LiteralPrefix))
        {
            return NormalizeMatrixToggle(body, context, DashCatalog.MatrixAxisXTogglePhrase.LiteralPrefix);
        }

        return body;
    }

    static bool StartsWithPhrasePrefix(string body, string prefix) =>
        body.Equals(prefix, StringComparison.OrdinalIgnoreCase)
        || body.StartsWith($"{prefix} ", StringComparison.OrdinalIgnoreCase);

    static string NormalizeMatrixToggle(string body, DashboardFilterContext context, string prefix)
    {
        if (!StartsWithPhrasePrefix(body, prefix))
        {
            return body;
        }

        var cardToken = body.Length <= prefix.Length
            ? string.Empty
            : body[(prefix.Length + 1)..].Trim();
        if (cardToken.Length == 0)
        {
            return body;
        }

        var card = DashboardCommandEntityResolver.ResolveMatrixCard(cardToken, context);
        return card is null ? body : $"{prefix} {card.CardId}";
    }

    static string NormalizeSelect(string[] tokens, DashboardFilterContext context)
    {
        if (tokens.Length < 2)
        {
            return string.Join(' ', tokens);
        }

        if (tokens[1].Equals(FilterCommandPaths.FilterBranch, StringComparison.OrdinalIgnoreCase)
            && tokens.Length >= 3)
        {
            var filterName = DashboardCommandEntityResolver.ResolveFilterName(tokens[2], context);
            if (filterName is null)
            {
                return string.Join(' ', tokens);
            }

            tokens[2] = filterName;
            return string.Join(' ', tokens);
        }

        if (tokens[1].Equals(FilterCommandPaths.ReportBranch, StringComparison.OrdinalIgnoreCase)
            && tokens.Length >= 3)
        {
            var entryId = DashboardCommandEntityResolver.ResolveCatalogEntryId(tokens[2], context);
            if (entryId is not null)
            {
                tokens[2] = entryId;
            }

            return string.Join(' ', tokens);
        }

        if (tokens[1].Equals(FilterCommandPaths.PageBranch, StringComparison.OrdinalIgnoreCase)
            && tokens.Length >= 3)
        {
            var pageId = DashboardCommandEntityResolver.ResolvePageId(tokens[2], context);
            if (pageId is not null)
            {
                tokens[2] = pageId;
            }

            return string.Join(' ', tokens);
        }

        return string.Join(' ', tokens);
    }

    static string NormalizeView(string[] tokens, DashboardFilterContext context)
    {
        if (tokens.Length < 2)
        {
            return string.Join(' ', tokens);
        }

        var card = DashboardCommandEntityResolver.ResolveCard(tokens[1], context);
        if (card is null)
        {
            return string.Join(' ', tokens);
        }

        tokens[1] = card.CardId;
        if (tokens.Length >= 3)
        {
            var viewId = DashboardCommandEntityResolver.ResolveViewId(card, tokens[2]);
            if (viewId is not null)
            {
                tokens[2] = viewId;
            }
        }

        return string.Join(' ', tokens);
    }
}
