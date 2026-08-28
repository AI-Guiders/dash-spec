namespace DashSpec.Core.Parsing;

/// <summary>Optional slash alias → filter id map (DASHSPEC-ADR-0043 §5).</summary>
internal static class CommandAliasesParser
{
    public static IReadOnlyDictionary<string, string> Parse(TokenReader reader)
    {
        BlockSyntax.BeginBlock(reader);
        reader.SkipNewlines();

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (!BlockSyntax.IsBlockEnd(reader, "commands") && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, "commands"))
            {
                break;
            }

            var alias = reader.ReadIdent();
            if (string.IsNullOrWhiteSpace(alias))
            {
                throw new DashSpecParseException("commands entry requires an alias name.");
            }

            reader.Expect(TokenKind.Eq);
            var filterId = reader.ReadIdent();
            if (string.IsNullOrWhiteSpace(filterId))
            {
                throw new DashSpecParseException($"commands alias '{alias}' requires a filter id.");
            }

            if (!map.TryAdd(alias, filterId))
            {
                throw new DashSpecParseException($"commands declares duplicate alias '{alias}'.");
            }

            reader.SkipNewlines();
        }

        BlockSyntax.ExpectBlockEnd(reader, "commands");
        return map;
    }
}
