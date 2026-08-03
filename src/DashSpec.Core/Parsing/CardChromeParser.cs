using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class CardChromeParser
{
    public static CardChromeDefinition Parse(TokenReader reader, string cardId)
    {
        BlockSyntax.BeginBlock(reader);
        CardBoundFilterChrome boundFilters = CardBoundFilterChrome.Chips;

        while (!BlockSyntax.IsBlockEnd(reader, "chrome"))
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, "chrome"))
            {
                break;
            }

            if (reader.TryKeyword("bound_filters"))
            {
                reader.Expect(TokenKind.Eq);
                boundFilters = reader.ReadIdent().Trim().ToLowerInvariant() switch
                {
                    "hidden" => CardBoundFilterChrome.Hidden,
                    "toolbar_only" or "toolbar-only" or "toolbaronly" => CardBoundFilterChrome.ToolbarOnly,
                    "chips" => CardBoundFilterChrome.Chips,
                    var raw => throw new DashSpecParseException(
                        $"Card '{cardId}': chrome bound_filters must be chips, hidden, or toolbar_only; got '{raw}'."),
                };

                reader.SkipNewlines();
                continue;
            }

            throw reader.Unexpected("chrome property");
        }

        BlockSyntax.ExpectBlockEnd(reader, "chrome");
        return new CardChromeDefinition(boundFilters);
    }
}

internal static class FilterDeriveParser
{
    public static FilterDeriveDefinition Parse(TokenReader reader, string pageId)
    {
        var target = reader.ReadIdent();
        if (!reader.TryKeyword("from"))
        {
            throw new DashSpecParseException($"Page '{pageId}': derive requires 'from <filter>'.");
        }

        var source = reader.ReadIdent();
        string? grainFilter = null;
        if (reader.TryKeyword("grain"))
        {
            grainFilter = reader.ReadIdent();
        }

        reader.SkipNewlines();
        return new FilterDeriveDefinition(target, source, grainFilter);
    }
}

internal static class ToolbarBoardFactory
{
    public static LayoutBoardDefinition FromFilterNames(IReadOnlyList<string> names)
    {
        if (names.Count == 0)
        {
            throw new DashSpecParseException("toolbar requires at least one filter name.");
        }

        return new LayoutBoardDefinition([names.ToList()]);
    }
}
