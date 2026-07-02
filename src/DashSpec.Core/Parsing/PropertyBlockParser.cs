namespace DashSpec.Core.Parsing;

internal enum PropertyValueType
{
    Scalar,
    String,
    DateRange,
    QualifiedName,
    CommaList,
    RestOfLine,
    ColumnBinding,
}

internal sealed class PropertySpec(string name, PropertyValueType valueType)
{
    public string Name { get; } = name;
    public PropertyValueType ValueType { get; } = valueType;
}

/// <summary>
/// Schema-driven parser for <c>{ key = value }</c> blocks.
/// Values are read with bounded grammars — never until arbitrary newline swallowing.
/// </summary>
internal static class PropertyBlockParser
{
    public static Dictionary<string, string> Parse(
        TokenReader reader,
        IReadOnlyList<PropertySpec> schema,
        string blockName,
        bool allowExtensionProperties = false,
        bool allowQuotedPropertyKeys = false)
    {
        reader.Expect(TokenKind.LBrace);
        reader.SkipNewlines();

        var specs = schema.ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.IsAt(TokenKind.RBrace))
            {
                break;
            }

            while (!reader.IsAt(TokenKind.Newline) && !reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
            {
                var key = reader.ReadPropertyKey(allowQuotedPropertyKeys);
                if (!specs.TryGetValue(key, out var spec))
                {
                    if (!allowExtensionProperties || key.EndsWith("_as", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new DashSpecParseException($"Unknown property '{key}' in {blockName} block.");
                    }

                    reader.Expect(TokenKind.Eq);
                    values[key] = reader.ReadScalarValue();
                    continue;
                }

                reader.Expect(TokenKind.Eq);
                if (spec.ValueType is PropertyValueType.ColumnBinding)
                {
                    var binding = reader.ReadColumnBinding();
                    values[key] = binding.Column;
                    if (binding.Alias is not null)
                    {
                        values[$"{key}_as"] = binding.Alias;
                    }
                }
                else
                {
                    values[key] = ReadTypedValue(reader, spec.ValueType);
                }
            }

            reader.SkipNewlines();
        }

        reader.Expect(TokenKind.RBrace);
        return values;
    }

    public static IReadOnlyList<string> ParseCommaListBlock(
        TokenReader reader,
        string blockName)
    {
        reader.Expect(TokenKind.LBrace);
        reader.SkipNewlines();

        var names = new List<string>();
        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.IsAt(TokenKind.RBrace))
            {
                break;
            }

            names.Add(reader.ReadIdent());
            reader.SkipNewlines();
            if (reader.CurrentKind is TokenKind.Comma)
            {
                reader.Advance();
            }
        }

        reader.SkipNewlines();
        reader.Expect(TokenKind.RBrace);
        if (names.Count == 0)
        {
            throw new DashSpecParseException($"{blockName} block requires at least one name.");
        }

        return names;
    }

    public static IReadOnlyList<string> ParseIdentListBlock(
        TokenReader reader,
        string blockName)
    {
        reader.Expect(TokenKind.LBrace);
        reader.SkipNewlines();

        var names = new List<string>();
        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.IsAt(TokenKind.RBrace))
            {
                break;
            }

            names.Add(reader.ReadIdent());
            reader.SkipNewlines();
            if (reader.CurrentKind is TokenKind.Comma)
            {
                reader.Advance();
            }
        }

        reader.SkipNewlines();
        reader.Expect(TokenKind.RBrace);
        if (names.Count == 0)
        {
            throw new DashSpecParseException($"{blockName} block requires at least one identifier.");
        }

        return names;
    }

    public static IReadOnlyList<string> ParseTitleListBlock(
        TokenReader reader,
        string blockName)
    {
        reader.Expect(TokenKind.LBrace);
        reader.SkipNewlines();

        var titles = new List<string>();
        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.IsAt(TokenKind.RBrace))
            {
                break;
            }

            titles.Add(ReadTitleToken(reader));
            reader.SkipNewlines();
            if (reader.CurrentKind is TokenKind.Comma)
            {
                reader.Advance();
            }
        }

        reader.SkipNewlines();
        reader.Expect(TokenKind.RBrace);
        if (titles.Count == 0)
        {
            throw new DashSpecParseException($"{blockName} block requires at least one title.");
        }

        return titles;
    }

    private static string ReadTitleToken(TokenReader reader) =>
        reader.CurrentKind switch
        {
            TokenKind.String => reader.ReadString(),
            TokenKind.Ident => reader.ReadIdent(),
            _ => throw reader.Unexpected("card title"),
        };

    private static string ReadTypedValue(TokenReader reader, PropertyValueType type) =>
        type switch
        {
            PropertyValueType.Scalar => reader.ReadScalarValue(),
            PropertyValueType.String => reader.ReadString(),
            PropertyValueType.DateRange => reader.ReadDateDefaultValue(),
            PropertyValueType.QualifiedName => reader.ReadQualifiedName(),
            PropertyValueType.CommaList => reader.ReadCommaSeparatedValues(),
            PropertyValueType.RestOfLine => reader.ReadRestOfLine(),
            _ => throw new DashSpecParseException($"Unsupported property value type '{type}'."),
        };
}

internal static class PropertySchemas
{
    public static IReadOnlyList<PropertySpec> LayoutGrid { get; } =
    [
        new("columns", PropertyValueType.Scalar),
        new("gap", PropertyValueType.Scalar),
    ];

    public static IReadOnlyList<PropertySpec> Placement { get; } =
    [
        new("row", PropertyValueType.Scalar),
        new("col", PropertyValueType.Scalar),
        new("span", PropertyValueType.Scalar),
    ];

    public static IReadOnlyList<PropertySpec> FilterDate { get; } =
    [
        new("column", PropertyValueType.ColumnBinding),
        new("default", PropertyValueType.DateRange),
        new("widget", PropertyValueType.Scalar),
        new("grain_filter", PropertyValueType.Scalar),
    ];

    public static IReadOnlyList<PropertySpec> FilterField { get; } =
    [
        new("column", PropertyValueType.ColumnBinding),
        new("widget", PropertyValueType.Scalar),
        new("default", PropertyValueType.Scalar),
        new("single", PropertyValueType.Scalar),
    ];

    public static IReadOnlyList<PropertySpec> FilterTop { get; } =
    [
        new("default", PropertyValueType.Scalar),
        new("min", PropertyValueType.Scalar),
        new("max", PropertyValueType.Scalar),
    ];

    public static IReadOnlyList<PropertySpec> FiltersChrome { get; } =
    [
        new("layout", PropertyValueType.Scalar),
        new("sticky", PropertyValueType.Scalar),
        new("apply", PropertyValueType.Scalar),
        new("debounce_ms", PropertyValueType.Scalar),
    ];

    public static IReadOnlyList<PropertySpec> Legend { get; } =
    [
        new("min", PropertyValueType.String),
        new("max", PropertyValueType.String),
        new("title", PropertyValueType.String),
    ];

    public static IReadOnlyList<PropertySpec> Presentation { get; } =
    [
        new("use", PropertyValueType.Scalar),
        new("legend", PropertyValueType.Scalar),
        new("height", PropertyValueType.Scalar),
        new("stacked", PropertyValueType.Scalar),
    ];

    public static IReadOnlyList<PropertySpec> SeriesTransform { get; } =
    [
        new("use", PropertyValueType.Scalar),
        new("max", PropertyValueType.Scalar),
        new("other", PropertyValueType.String),
    ];

    public static IReadOnlyList<PropertySpec> Palette { get; } =
    [
        new("colors", PropertyValueType.String),
        new("default", PropertyValueType.String),
    ];
}
