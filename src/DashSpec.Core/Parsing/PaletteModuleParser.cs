namespace DashSpec.Core.Parsing;

internal static class PaletteModuleParser
{
    public static (string Id, IReadOnlyDictionary<string, string> Properties) ParsePaletteFile(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var reader = ParserUtilities.CreateReader(text);
        reader.SkipFileDirectives();
        reader.Expect(TokenKind.At);
        reader.ExpectKeyword("palette");
        var id = reader.ReadIdent();
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new DashSpecParseException("Palette module requires @palette <id>.");
        }

        reader.SkipNewlines();
        var constants = ParseConstants(reader);

        Dictionary<string, string> props;
        if (reader.TryKeyword("palette"))
        {
            if (reader.IsOnNewline())
            {
                BlockSyntax.BeginBlock(reader);
                reader.SkipNewlines();
                props = ParsePaletteMappingsUntilEnd(reader, constants, "palette");
            }
            else if (reader.IsAt(TokenKind.LBrace))
            {
                reader.Expect(TokenKind.LBrace);
                reader.SkipNewlines();
                props = ParsePaletteMappings(reader, constants, wrapped: true);
                reader.Expect(TokenKind.RBrace);
            }
            else
            {
                props = ParsePaletteMappings(reader, constants, wrapped: false);
            }
        }
        else
        {
            props = ParsePaletteMappings(reader, constants, wrapped: false);
        }

        if (props.Count == 0)
        {
            throw new DashSpecParseException($"Palette '{id}' requires at least one mapping entry.");
        }

        return (id, props);
    }

    public static SpecLibrary LoadPaletteFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Palette file not found: {path}", path);
        }

        var (id, props) = ParsePaletteFile(File.ReadAllText(path));
        return SpecLibrary.FromPalette(id, props);
    }

    private static Dictionary<string, string> ParseConstants(TokenReader reader)
    {
        var constants = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (reader.TryKeyword("const"))
        {
            var name = reader.ReadIdent();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new DashSpecParseException("const requires a name.");
            }

            reader.Expect(TokenKind.Eq);
            var operand = ReadColorOperand(reader);
            var hex = PaletteColorResolver.ResolveOperand(
                operand,
                constants,
                $"const '{name}'");
            constants[name] = hex;
            reader.SkipNewlines();
        }

        return constants;
    }

    private static Dictionary<string, string> ParsePaletteMappings(
        TokenReader reader,
        IReadOnlyDictionary<string, string> constants,
        bool wrapped)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while ((!wrapped && !reader.IsEof) ||
               (wrapped && !reader.IsAt(TokenKind.RBrace) && !reader.IsEof))
        {
            reader.SkipNewlines();
            if (wrapped && reader.IsAt(TokenKind.RBrace))
            {
                break;
            }

            if (!wrapped && reader.IsEof)
            {
                break;
            }

            while (!reader.IsAt(TokenKind.Newline) &&
                   !reader.IsEof &&
                   (!wrapped || !reader.IsAt(TokenKind.RBrace)))
            {
                var key = reader.ReadPropertyKey(allowQuoted: true);
                reader.Expect(TokenKind.Eq);

                if (key.Equals("colors", StringComparison.OrdinalIgnoreCase))
                {
                    values[key] = ReadColorsProperty(reader, constants);
                    continue;
                }

                var operand = ReadColorOperand(reader);
                values[key] = PaletteColorResolver.ResolveOperand(
                    operand,
                    constants,
                    $"palette entry '{key}'");
            }

            reader.SkipNewlines();
        }

        return values;
    }

    private static Dictionary<string, string> ParsePaletteMappingsUntilEnd(
        TokenReader reader,
        IReadOnlyDictionary<string, string> constants,
        string endKind)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (!BlockSyntax.IsBlockEnd(reader, endKind) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, endKind))
            {
                break;
            }

            while (!reader.IsAt(TokenKind.Newline) &&
                   !reader.IsEof &&
                   !BlockSyntax.IsBlockEnd(reader, endKind))
            {
                var key = reader.ReadPropertyKey(allowQuoted: true);
                reader.Expect(TokenKind.Eq);

                if (key.Equals("colors", StringComparison.OrdinalIgnoreCase))
                {
                    values[key] = ReadColorsProperty(reader, constants);
                    continue;
                }

                var operand = ReadColorOperand(reader);
                values[key] = PaletteColorResolver.ResolveOperand(
                    operand,
                    constants,
                    $"palette entry '{key}'");
            }

            reader.SkipNewlines();
        }

        BlockSyntax.ExpectBlockEnd(reader, endKind);
        return values;
    }

    private static string ReadColorsProperty(TokenReader reader, IReadOnlyDictionary<string, string> constants)
    {
        if (reader.IsAt(TokenKind.LBracket))
        {
            var operands = ReadColorList(reader);
            var resolved = operands
                .Select(operand => PaletteColorResolver.ResolveOperand(operand, constants, "colors list"))
                .ToList();
            return PaletteColorResolver.JoinColorList(resolved);
        }

        if (reader.CurrentKind is TokenKind.String)
        {
            return reader.ReadString();
        }

        throw reader.Unexpected("colors list [ … ] or string");
    }

    private static List<string> ReadColorList(TokenReader reader)
    {
        reader.Expect(TokenKind.LBracket);
        reader.SkipNewlines();

        var items = new List<string>();
        while (!reader.IsAt(TokenKind.RBracket) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.IsAt(TokenKind.RBracket))
            {
                break;
            }

            items.Add(ReadColorOperand(reader));
            reader.SkipNewlines();
            if (reader.CurrentKind is TokenKind.Comma)
            {
                reader.Advance();
            }
        }

        reader.SkipNewlines();
        reader.Expect(TokenKind.RBracket);
        if (items.Count == 0)
        {
            throw new DashSpecParseException("colors list requires at least one entry.");
        }

        return items;
    }

    private static string ReadColorOperand(TokenReader reader)
    {
        reader.SkipNewlines();
        return reader.CurrentKind switch
        {
            TokenKind.String => reader.ReadString(),
            TokenKind.Ident => reader.ReadIdent(),
            _ => throw reader.Unexpected("color literal, CSS name, or const reference"),
        };
    }
}
