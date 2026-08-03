using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal sealed class TokenReader
{
    private readonly IReadOnlyList<Token> _tokens;
    private readonly Stack<BlockCloseStyle> _blockCloseStyles = new();
    private int _index;

    public TokenReader(IReadOnlyList<Token> tokens) => _tokens = tokens;

    internal void PushBlockClose(BlockCloseStyle style) => _blockCloseStyles.Push(style);

    internal void PopBlockClose()
    {
        if (_blockCloseStyles.Count > 0)
        {
            _blockCloseStyles.Pop();
        }
    }

    internal BlockCloseStyle PeekBlockClose() =>
        _blockCloseStyles.Count > 0 ? _blockCloseStyles.Peek() : BlockCloseStyle.EndKeyword;

    public string? ConsumedRuntimePath { get; private set; }

    /// <summary>Deprecated alias; use <see cref="ConsumedRuntimePath"/>.</summary>
    public string? ConsumedConfigPath => ConsumedRuntimePath;

    public SqlDialect ConsumedSqlDialect { get; private set; } = SqlDialect.TSql;

    public string? ConsumedDiagramLibraryPath { get; private set; }

    public string? ConsumedPalettePath { get; private set; }

    public void SkipFileDirectives()
    {
        SkipNewlines();
        while (IsAt(TokenKind.At))
        {
            Advance();
            if (TryKeyword("runtime") || TryKeyword("config"))
            {
                if (ConsumedRuntimePath is not null)
                {
                    throw new DashSpecParseException(
                        "Only one @runtime directive is allowed per .dashspec file.");
                }

                ConsumedRuntimePath = ReadString();
                SkipNewlines();
                continue;
            }

            if (TryKeyword("sqldialect"))
            {
                ConsumedSqlDialect = SqlDialectParser.Parse(ReadIdent());
                SkipNewlines();
                continue;
            }

            if (TryKeyword("diagramlibrary"))
            {
                if (ConsumedDiagramLibraryPath is not null)
                {
                    throw new DashSpecParseException(
                        "Only one @diagramlibrary directive is allowed per .dashspec file.");
                }

                ConsumedDiagramLibraryPath = ReadString();
                SkipNewlines();
                continue;
            }

            if (TryKeyword("palette"))
            {
                SkipNewlines();
                if (Current.Kind is TokenKind.String)
                {
                    if (ConsumedPalettePath is not null)
                    {
                        throw new DashSpecParseException(
                            "Only one @palette file directive is allowed per .dashspec file.");
                    }

                    ConsumedPalettePath = ReadString();
                    SkipNewlines();
                    continue;
                }

                _index -= 2;
                break;
            }

            _index--;
            break;
        }
    }

    public string ReadPropertyKey(bool allowQuoted = false)
    {
        SkipNewlines();
        if (allowQuoted && Current.Kind is TokenKind.String)
        {
            return ReadString();
        }

        return ReadIdent();
    }

    public bool IsEof => Current.Kind is TokenKind.Eof;

    public TokenKind CurrentKind
    {
        get
        {
            SkipNewlines();
            return Current.Kind;
        }
    }

    private Token Current => _tokens[_index];

    /// <summary>Current token kind without skipping newlines (use for same-line inline grammar).</summary>
    public TokenKind RawKind => Current.Kind;

    public bool IsOnNewline() => Current.Kind is TokenKind.Newline;

    public void SkipNewlines()
    {
        while (Current.Kind is TokenKind.Newline)
        {
            _index++;
        }
    }

    public void Advance() => _index++;

    public bool IsAt(TokenKind kind)
    {
        SkipNewlines();
        return Current.Kind == kind;
    }

    public void Expect(TokenKind kind)
    {
        SkipNewlines();
        if (Current.Kind != kind)
        {
            throw Unexpected(kind.ToString());
        }

        _index++;
    }

    public bool TryKeyword(string keyword)
    {
        SkipNewlines();
        if (Current.Kind is not TokenKind.Ident ||
            !string.Equals(Current.Value, keyword, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _index++;
        return true;
    }

    public bool TryPeekIdent(out string ident)
    {
        SkipNewlines();
        if (Current.Kind is not TokenKind.Ident)
        {
            ident = string.Empty;
            return false;
        }

        ident = Current.Value;
        return true;
    }

    public readonly struct Mark
    {
        internal Mark(int index) => Index = index;
        internal int Index { get; }
    }

    public Mark CreateMark() => new(_index);

    public void Rewind(Mark mark) => _index = mark.Index;

    public bool TryModuleInclude(out string reference)
    {
        SkipNewlines();
        if (!IsAt(TokenKind.Bang))
        {
            reference = string.Empty;
            return false;
        }

        Advance();
        if (!TryKeyword("include"))
        {
            throw new DashSpecParseException("Expected 'include' after '!'.");
        }

        reference = ReadString();
        return true;
    }

    /// <summary>
    /// Optional postfix keyword on the current line only (does not cross <see cref="TokenKind.Newline"/>).
    /// Use for <c>ref</c>, optional trailing <c>as</c>, etc. — never for statement-start keywords.
    /// </summary>
    public bool TryKeywordSameLine(string keyword)
    {
        if (Current.Kind is TokenKind.Newline or TokenKind.Eof)
        {
            return false;
        }

        if (Current.Kind is not TokenKind.Ident ||
            !string.Equals(Current.Value, keyword, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _index++;
        return true;
    }

    public string ReadIdentSameLine()
    {
        if (Current.Kind is not TokenKind.Ident)
        {
            throw Unexpected("identifier on the same line");
        }

        var value = Current.Value;
        _index++;
        return value;
    }

    public void ExpectKeyword(string keyword)
    {
        if (!TryKeyword(keyword))
        {
            throw Unexpected(keyword);
        }
    }

    public string ReadIdent()
    {
        SkipNewlines();
        if (Current.Kind is not TokenKind.Ident)
        {
            throw Unexpected("identifier");
        }

        var value = Current.Value;
        _index++;
        return value;
    }

    public string ReadQualifiedName()
    {
        SkipNewlines();
        var value = Current.Kind switch
        {
            TokenKind.Ident => Current.Value,
            TokenKind.Raw => Current.Value,
            _ => throw Unexpected("qualified name"),
        };

        _index++;
        return value;
    }

    public string ReadString()
    {
        SkipNewlines();
        if (Current.Kind is not TokenKind.String)
        {
            throw Unexpected("string");
        }

        var value = Current.Value;
        _index++;
        return value;
    }

    public string ReadScalarValue()
    {
        SkipNewlines();
        return Current.Kind switch
        {
            TokenKind.String => ReadString(),
            TokenKind.Ident => ReadIdent(),
            TokenKind.RelativeDay => ReadRelativeDay(),
            _ => throw Unexpected("scalar value"),
        };
    }

    public string ReadRawBlock()
    {
        SkipNewlines();
        if (Current.Kind is not TokenKind.Raw)
        {
            throw Unexpected("[[ … ]] raw block");
        }

        var value = Current.Value;
        _index++;
        return value;
    }

    public ColumnBindingValue ReadColumnBinding()
    {
        var column = ReadQualifiedName();
        if (TryKeyword("as"))
        {
            return new ColumnBindingValue(column, ReadString());
        }

        if (RawKind is TokenKind.Ident && !IsOnNewline())
        {
            var saved = SavePosition();
            var alias = ReadIdent();
            if (RawKind is TokenKind.Eq)
            {
                RestorePosition(saved);
                return new ColumnBindingValue(column, null);
            }

            if (string.Equals(alias, "end", StringComparison.OrdinalIgnoreCase))
            {
                RestorePosition(saved);
                return new ColumnBindingValue(column, null);
            }

            return new ColumnBindingValue(column, alias);
        }

        return new ColumnBindingValue(column, null);
    }

    public string ReadCommaSeparatedValues()
    {
        var parts = new List<string> { ReadListItem() };
        while (CurrentKind is TokenKind.Comma)
        {
            _index++;
            parts.Add(ReadListItem());
        }

        return string.Join(", ", parts);
    }

    public IReadOnlyList<string> ReadCommaListInline()
    {
        var names = new List<string> { ReadIdent() };
        while (CurrentKind is TokenKind.Comma)
        {
            _index++;
            names.Add(ReadIdent());
        }

        return names;
    }

    public string ReadDateDefaultValue()
    {
        var from = ReadDateBoundToken();
        if (RawKind is not TokenKind.DotDot)
        {
            return from;
        }

        _index++;
        var to = ReadDateBoundToken();
        return $"{from}..{to}";
    }

    private string ReadDateBoundToken()
    {
        SkipNewlines();
        return Current.Kind switch
        {
            TokenKind.RelativeDay => ReadRelativeDay(),
            TokenKind.Ident => ReadIdent(),
            _ => throw Unexpected("date bound (today, -Nd, yyyy-MM-dd)"),
        };
    }

    private string ReadRelativeDay()
    {
        SkipNewlines();
        if (Current.Kind is not TokenKind.RelativeDay)
        {
            throw Unexpected("relative day");
        }

        var value = Current.Value;
        _index++;
        return value;
    }

    private string ReadListItem()
    {
        SkipNewlines();
        return Current.Kind switch
        {
            TokenKind.Ident => ReadIdent(),
            TokenKind.String => ReadString(),
            _ => throw Unexpected("list item"),
        };
    }

    /// <summary>Reads scalar tokens until newline/EOF — does not cross line boundaries.</summary>
    public string ReadRestOfLine()
    {
        var parts = new List<string>();
        while (Current.Kind is TokenKind.Ident or TokenKind.Raw)
        {
            parts.Add(Current.Value);
            _index++;
        }

        return string.Join(' ', parts);
    }

    public DashSpecParseException Unexpected(string? expected = null)
    {
        var token = Current;
        var message = expected is null
            ? $"Unexpected token '{token.Value}' ({token.Kind}) at position {token.Start}."
            : $"Expected {expected}, got '{token.Value}' ({token.Kind}) at position {token.Start}.";
        return new DashSpecParseException(message, token.Start);
    }

    public int SavePosition() => _index;

    public void RestorePosition(int position) => _index = position;
}
