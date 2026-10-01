using DashSpec.Modeling.Parse.Lexing;

namespace DashSpec.Core.Parsing;

/// <summary>Dual block syntax facade over F# <see cref="Modeling.Parse.BlockSyntax"/> (ADR-0036).</summary>
internal static class BlockSyntax
{
    public static void BeginBlock(TokenReader reader) =>
        Modeling.Parse.BlockSyntax.beginBlock(reader);

    public static bool IsBlockEnd(TokenReader reader, string endKind, string? endId = null) =>
        Modeling.Parse.BlockSyntax.isBlockEnd(reader, endKind, endId);

    public static void ExpectBlockEnd(TokenReader reader, string endKind, string? endId = null) =>
        Modeling.Parse.BlockSyntax.expectBlockEnd(reader, endKind, endId);
}
