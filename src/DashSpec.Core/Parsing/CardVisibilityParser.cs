using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class CardVisibilityParser
{
    public static CardVisibilityRule ParseFilterWhen(TokenReader reader, string cardId, string filterName)
    {
        if (string.IsNullOrWhiteSpace(filterName))
        {
            throw new DashSpecParseException($"Card '{cardId}': when requires filter name or oversize.");
        }

        if (reader.TryKeyword("empty"))
        {
            return new CardVisibilityRule(filterName, CardVisibilityMode.WhenEmpty);
        }

        if (reader.TryKeyword("set"))
        {
            reader.SkipNewlines();
            return new CardVisibilityRule(
                filterName,
                CardVisibilityMode.WhenSet,
                TryReadMessageBody(reader, cardId, filterName));
        }

        reader.SkipNewlines();
        if (!reader.IsEof && reader.TryKeyword("message"))
        {
            reader.Expect(TokenKind.Eq);
            var inlineMessage = reader.ReadString();
            reader.SkipNewlines();
            ExpectWhenBlockEnd(reader, filterName);
            return new CardVisibilityRule(filterName, CardVisibilityMode.WhenSet, inlineMessage);
        }

        if (!reader.IsEof && !IsWhenBlockEnd(reader, filterName))
        {
            return new CardVisibilityRule(
                filterName,
                CardVisibilityMode.WhenSet,
                ReadMessageBody(reader, cardId, filterName));
        }

        return new CardVisibilityRule(filterName, CardVisibilityMode.WhenSet);
    }

    public static string ParseOversizeWhen(TokenReader reader, string cardId)
    {
        reader.SkipNewlines();
        return ReadMessageBody(reader, cardId, "oversize");
    }

    private static string? TryReadMessageBody(TokenReader reader, string cardId, string filterName) =>
        reader.IsEof || IsWhenBlockEnd(reader, filterName)
            ? null
            : ReadMessageBody(reader, cardId, filterName);

    private static string ReadMessageBody(TokenReader reader, string cardId, string filterName)
    {
        reader.SkipNewlines();
        string? message = null;

        while (!IsWhenBlockEnd(reader, filterName) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (IsWhenBlockEnd(reader, filterName))
            {
                break;
            }

            var key = reader.ReadIdent();
            if (!string.Equals(key, "message", StringComparison.OrdinalIgnoreCase))
            {
                throw new DashSpecParseException(
                    $"Card '{cardId}': when block supports only message = \"…\".");
            }

            reader.Expect(TokenKind.Eq);
            message = reader.ReadString();
            reader.SkipNewlines();
        }

        ExpectWhenBlockEnd(reader, filterName);

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new DashSpecParseException($"Card '{cardId}': when message cannot be empty.");
        }

        return message;
    }

    private static bool IsWhenBlockEnd(TokenReader reader, string? filterName = null) =>
        BlockSyntax.IsBlockEnd(reader, "when") ||
        BlockSyntax.IsBlockEnd(reader, "oversize") ||
        (filterName is not null && BlockSyntax.IsBlockEnd(reader, filterName));

    private static void ExpectWhenBlockEnd(TokenReader reader, string? filterName = null)
    {
        if (filterName is not null && BlockSyntax.IsBlockEnd(reader, filterName))
        {
            BlockSyntax.ExpectBlockEnd(reader, filterName);
            return;
        }

        if (BlockSyntax.IsBlockEnd(reader, "oversize"))
        {
            BlockSyntax.ExpectBlockEnd(reader, "oversize");
            return;
        }

        BlockSyntax.ExpectBlockEnd(reader, "when");
    }
}
