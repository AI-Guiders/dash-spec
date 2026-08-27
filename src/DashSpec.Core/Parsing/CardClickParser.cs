using DashSpec.Abstractions.Plugins;
using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class CardClickParser
{
    public static CardClickBehaviour ParseClickBlock(
        TokenReader reader,
        string cardId,
        DashSpecParseOptions parseOptions)
    {
        BlockSyntax.BeginBlock(reader);
        reader.SkipNewlines();

        var effects = new List<CardClickEffect>();
        var phraseTemplates = parseOptions.PhraseTemplates
            .Where(x => string.Equals(x.Scope, PhraseScopes.OnClick, StringComparison.OrdinalIgnoreCase))
            .ToList();

        while (!BlockSyntax.IsBlockEnd(reader, "click") && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, "click"))
            {
                break;
            }

            if (reader.TryKeyword("show"))
            {
                effects.Add(ParseShowEffect(reader, cardId));
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("set"))
            {
                effects.Add(ParseSetEffect(reader, cardId));
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("goto"))
            {
                effects.Add(ParseGotoEffect(reader, cardId));
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("focus"))
            {
                effects.Add(ParseFocusEffect(reader, cardId));
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("invoke") || reader.TryKeyword("run"))
            {
                effects.Add(ParseInvokeEffect(reader, cardId, parseOptions));
                reader.SkipNewlines();
                continue;
            }

            if (TryParsePhraseEffect(reader, cardId, phraseTemplates, out var phraseEffect))
            {
                effects.Add(phraseEffect);
                reader.SkipNewlines();
                continue;
            }

            throw reader.Unexpected();
        }

        BlockSyntax.ExpectBlockEnd(reader, "click");

        if (effects.Count == 0)
        {
            throw new DashSpecParseException($"Card '{cardId}': on click block requires at least one effect.");
        }

        return new CardClickBehaviour(effects);
    }

    private static bool TryParsePhraseEffect(
        TokenReader reader,
        string cardId,
        IReadOnlyList<PhraseTemplateDescriptor> templates,
        out InvokeHandlerEffect effect)
    {
        effect = null!;
        if (templates.Count == 0)
        {
            return false;
        }

        var tokens = PhraseLineReader.ReadLineTokens(reader);
        if (tokens.Count == 0)
        {
            return false;
        }

        if (!PhraseTemplateMatcher.TryMatchAny(tokens, templates, out var matched, out var args))
        {
            throw new DashSpecParseException(
                $"Card '{cardId}': unrecognized phrase in on click block.");
        }

        effect = new InvokeHandlerEffect(matched.HandlerId, args);
        return true;
    }

    private static InvokeHandlerEffect ParseInvokeEffect(TokenReader reader, string cardId, DashSpecParseOptions parseOptions)
    {
        var handlerId = reader.ReadIdent();
        if (string.IsNullOrWhiteSpace(handlerId))
        {
            throw new DashSpecParseException($"Card '{cardId}': invoke requires handler id.");
        }

        ValidateKnownHandler(cardId, handlerId, parseOptions);

        var args = ParseCallArgs(reader, cardId);
        return new InvokeHandlerEffect(handlerId, args);
    }

    private static void ValidateKnownHandler(string cardId, string handlerId, DashSpecParseOptions parseOptions)
    {
        if (parseOptions.KnownActionHandlers.Count == 0 &&
            parseOptions.KnownInteractionHandlers.Count == 0)
        {
            return;
        }

        if (parseOptions.KnownActionHandlers.Contains(handlerId) ||
            parseOptions.KnownInteractionHandlers.Contains(handlerId))
        {
            return;
        }

        throw new DashSpecParseException(
            $"Card '{cardId}': unknown handler '{handlerId}'.");
    }

    private static Dictionary<string, string> ParseCallArgs(TokenReader reader, string cardId)
    {
        var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!reader.IsAt(TokenKind.LParen))
        {
            return args;
        }

        reader.Expect(TokenKind.LParen);
        reader.SkipNewlines();

        while (!reader.IsAt(TokenKind.RParen) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.IsAt(TokenKind.RParen))
            {
                break;
            }

            var name = reader.ReadIdent();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new DashSpecParseException($"Card '{cardId}': expected argument name in invoke/run call.");
            }

            reader.Expect(TokenKind.Eq);
            args[name] = reader.ReadScalarValue();
            reader.SkipNewlines();

            if (reader.IsAt(TokenKind.Comma))
            {
                reader.Advance();
            }
        }

        reader.Expect(TokenKind.RParen);
        return args;
    }

    private static ShowSelectionEffect ParseShowEffect(TokenReader reader, string cardId)
    {
        if (!reader.TryKeyword("below"))
        {
            throw new DashSpecParseException(
                $"Card '{cardId}': show supports only 'below' placement in v1.");
        }

        if (!reader.TryKeyword("as"))
        {
            throw new DashSpecParseException(
                $"Card '{cardId}': show below requires 'as list|plain|kv'.");
        }

        var formatToken = reader.ReadIdent();
        if (string.IsNullOrWhiteSpace(formatToken))
        {
            throw new DashSpecParseException(
                $"Card '{cardId}': show below requires 'as list|plain|kv'.");
        }

        var format = formatToken.ToLowerInvariant() switch
        {
            "list" => ShowFormat.List,
            "plain" => ShowFormat.Plain,
            "kv" => ShowFormat.Kv,
            _ => throw new DashSpecParseException(
                $"Card '{cardId}': show format must be list, plain, or kv; got '{formatToken}'."),
        };

        if (!reader.TryKeyword("from"))
        {
            throw new DashSpecParseException(
                $"Card '{cardId}': show below requires 'from tooltip|cell'.");
        }

        var sourceToken = reader.ReadIdent();
        if (string.IsNullOrWhiteSpace(sourceToken))
        {
            throw new DashSpecParseException(
                $"Card '{cardId}': show below requires 'from tooltip|cell'.");
        }

        var source = sourceToken.ToLowerInvariant() switch
        {
            "tooltip" => ShowSource.Tooltip,
            "cell" => ShowSource.Cell,
            _ => throw new DashSpecParseException(
                $"Card '{cardId}': show source must be tooltip or cell; got '{sourceToken}'."),
        };

        var copyFriendly = reader.TryKeyword("copy");

        string? split = null;
        if (reader.TryKeyword("split"))
        {
            if (reader.IsAt(TokenKind.Eq))
            {
                reader.Expect(TokenKind.Eq);
            }

            split = reader.ReadString();
        }

        return new ShowSelectionEffect(ShowPlacement.Below, format, source, copyFriendly, split);
    }

    private static SetFilterFromFieldEffect ParseSetEffect(TokenReader reader, string cardId)
    {
        var filterName = reader.ReadIdent();
        if (string.IsNullOrWhiteSpace(filterName))
        {
            throw new DashSpecParseException($"Card '{cardId}': set requires filter name.");
        }

        if (!reader.TryKeyword("from"))
        {
            throw new DashSpecParseException($"Card '{cardId}': set {filterName} requires 'from x|y|value'.");
        }

        var field = reader.ReadIdent();
        if (string.IsNullOrWhiteSpace(field))
        {
            throw new DashSpecParseException($"Card '{cardId}': set {filterName} requires 'from x|y|value'.");
        }

        if (!field.Equals("x", StringComparison.OrdinalIgnoreCase) &&
            !field.Equals("y", StringComparison.OrdinalIgnoreCase) &&
            !field.Equals("value", StringComparison.OrdinalIgnoreCase))
        {
            throw new DashSpecParseException(
                $"Card '{cardId}': set from field must be x, y, or value; got '{field}'.");
        }

        return new SetFilterFromFieldEffect(filterName, field.ToLowerInvariant());
    }

    private static CardClickEffect ParseGotoEffect(TokenReader reader, string cardId)
    {
        if (reader.TryKeyword("tab"))
        {
            var tabId = reader.ReadIdent();
            if (string.IsNullOrWhiteSpace(tabId))
            {
                throw new DashSpecParseException($"Card '{cardId}': goto tab requires tab id.");
            }

            return new GotoTabEffect(tabId);
        }

        if (reader.TryKeyword("page"))
        {
            var pageId = reader.ReadIdent();
            if (string.IsNullOrWhiteSpace(pageId))
            {
                throw new DashSpecParseException($"Card '{cardId}': goto page requires page id.");
            }

            return new GotoPageEffect(pageId);
        }

        if (reader.TryKeyword("entry"))
        {
            var entryId = reader.ReadIdent();
            if (string.IsNullOrWhiteSpace(entryId))
            {
                throw new DashSpecParseException($"Card '{cardId}': goto entry requires catalog entry id.");
            }

            IReadOnlyList<string>? preserveFilterNames = null;
            if (reader.TryKeyword("preserving"))
            {
                if (!reader.TryKeyword("filters"))
                {
                    throw new DashSpecParseException(
                        $"Card '{cardId}': goto entry preserving requires 'filters' keyword.");
                }

                preserveFilterNames = ParsePreserveFilterList(reader);
            }

            return new GotoCatalogEntryEffect(entryId, preserveFilterNames);
        }

        throw new DashSpecParseException($"Card '{cardId}': goto requires tab, page, or entry.");
    }

    private static IReadOnlyList<string> ParsePreserveFilterList(TokenReader reader)
    {
        reader.SkipNewlines();
        if (reader.IsEof)
        {
            return [];
        }

        if (reader.TryPeekIdent(out var next) &&
            string.Equals(next, "end", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return reader.ReadCommaListInline();
    }

    private static FocusPhaseEffect ParseFocusEffect(TokenReader reader, string cardId)
    {
        var phaseId = reader.ReadIdent();
        if (string.IsNullOrWhiteSpace(phaseId))
        {
            throw new DashSpecParseException($"Card '{cardId}': focus requires phase id.");
        }

        return new FocusPhaseEffect(phaseId);
    }
}
