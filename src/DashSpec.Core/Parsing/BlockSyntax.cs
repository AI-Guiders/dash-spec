namespace DashSpec.Core.Parsing;

internal enum BlockCloseStyle
{
    Brace,
    EndKeyword,
}

/// <summary>Dual block syntax: legacy <c>{ … }</c> or <c>… end kind</c> (ADR-0036).</summary>
internal static class BlockSyntax
{
    public static void BeginBlock(TokenReader reader)
    {
        reader.SkipNewlines();
        if (reader.IsAt(TokenKind.LBrace))
        {
            reader.Advance();
            reader.PushBlockClose(BlockCloseStyle.Brace);
            return;
        }

        reader.PushBlockClose(BlockCloseStyle.EndKeyword);
    }

    public static bool IsBlockEnd(
        TokenReader reader,
        string endKind,
        string? endId = null)
    {
        return reader.PeekBlockClose() switch
        {
            BlockCloseStyle.Brace => reader.IsAt(TokenKind.RBrace),
            _ => TryMatchEnd(reader, endKind, endId, consume: false),
        };
    }

    public static void ExpectBlockEnd(
        TokenReader reader,
        string endKind,
        string? endId = null)
    {
        if (reader.PeekBlockClose() is BlockCloseStyle.Brace)
        {
            reader.Expect(TokenKind.RBrace);
            reader.PopBlockClose();
            return;
        }

        if (!TryMatchEnd(reader, endKind, endId, consume: true))
        {
            var label = endId is null ? endKind : $"{endKind} {endId}";
            throw new DashSpecParseException($"Expected end {label}.");
        }

        reader.PopBlockClose();
    }

    public static bool TryMatchEnd(
        TokenReader reader,
        string expectedKind,
        string? expectedId,
        bool consume)
    {
        reader.SkipNewlines();
        var index = reader.SavePosition();
        if (!reader.TryKeyword("end"))
        {
            return false;
        }

        var actualKind = ReadEndKind(reader);
        if (!string.Equals(actualKind, expectedKind, StringComparison.OrdinalIgnoreCase))
        {
            reader.RestorePosition(index);
            return false;
        }

        string? actualId = null;
        if (reader.RawKind is TokenKind.Ident && !reader.IsOnNewline())
        {
            actualId = reader.ReadIdentSameLine();
        }

        if (expectedId is not null &&
            actualId is not null &&
            !string.Equals(actualId, expectedId, StringComparison.OrdinalIgnoreCase))
        {
            reader.RestorePosition(index);
            return false;
        }

        if (!consume)
        {
            reader.RestorePosition(index);
        }

        return true;
    }

    private static string ReadEndKind(TokenReader reader)
    {
        if (reader.TryKeyword("on"))
        {
            reader.ExpectKeyword("click");
            return "click";
        }

        return reader.ReadIdent();
    }
}
