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
        bool allowQuotedPropertyKeys = false,
        string? endKind = null) =>
        ParseContainer(reader, schema, blockName, endKind ?? ResolveEndKind(blockName), allowExtensionProperties, allowQuotedPropertyKeys);

    private static string ResolveEndKind(string blockName)
    {
        var parts = blockName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return blockName;
        }

        return parts[0] switch
        {
            "transform" => "transform",
            "filters" when parts.Length > 1 && parts[1] is "chrome" => "chrome",
            "filter" => "filter",
            "bind" => "bind",
            "diagram" => parts[^1],
            "layout" when parts.Length > 1 && parts[1] is "grid" => "grid",
            "layout" => "layout",
            "place" => "place",
            "series" => "series",
            "toolbar" => parts[^1],
            _ => parts.Length > 1 ? parts[^1] : blockName,
        };
    }

    public static Dictionary<string, string> ParseContainer(
        TokenReader reader,
        IReadOnlyList<PropertySpec> schema,
        string blockName,
        string endKind,
        bool allowExtensionProperties = false,
        bool allowQuotedPropertyKeys = false)
    {
        BlockSyntax.BeginBlock(reader);
        reader.SkipNewlines();

        var specs = schema.ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (!BlockSyntax.IsBlockEnd(reader, endKind) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, endKind))
            {
                break;
            }

            while (!reader.IsOnNewline() && !BlockSyntax.IsBlockEnd(reader, endKind) && !reader.IsEof)
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

                if (string.Equals(key, "use", StringComparison.OrdinalIgnoreCase) &&
                    !reader.IsAt(TokenKind.Eq) &&
                    reader.TryPeekIdent(out _))
                {
                    values[key] = reader.ReadIdent();
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

        BlockSyntax.ExpectBlockEnd(reader, endKind);
        return values;
    }

    /// <summary>Flat key = value lines until EOF (fragment files without inner keyword wrapper).</summary>
    public static Dictionary<string, string> ParseFlatProperties(
        TokenReader reader,
        IReadOnlyList<PropertySpec> schema,
        string context,
        bool allowExtensionProperties = false,
        bool allowQuotedPropertyKeys = false)
    {
        var specs = schema.ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (!reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.IsEof)
            {
                break;
            }

            while (!reader.IsAt(TokenKind.Newline) && !reader.IsEof)
            {
                var key = reader.ReadPropertyKey(allowQuotedPropertyKeys);
                if (!specs.TryGetValue(key, out var spec))
                {
                    if (!allowExtensionProperties)
                    {
                        throw new DashSpecParseException($"Unknown property '{key}' in {context}.");
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

        return values;
    }

    public static IReadOnlyList<string> ParseCommaListBlock(
        TokenReader reader,
        string endKind,
        string blockName)
    {
        BlockSyntax.BeginBlock(reader);
        reader.SkipNewlines();

        var names = new List<string>();
        while (!BlockSyntax.IsBlockEnd(reader, endKind) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, endKind))
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
        BlockSyntax.ExpectBlockEnd(reader, endKind);
        if (names.Count == 0)
        {
            throw new DashSpecParseException($"{blockName} block requires at least one name.");
        }

        return names;
    }

    public static IReadOnlyList<string> ParseIdentListBlock(
        TokenReader reader,
        string endKind,
        string blockName)
    {
        BlockSyntax.BeginBlock(reader);
        reader.SkipNewlines();

        var names = new List<string>();
        while (!BlockSyntax.IsBlockEnd(reader, endKind) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, endKind))
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
        BlockSyntax.ExpectBlockEnd(reader, endKind);
        if (names.Count == 0)
        {
            throw new DashSpecParseException($"{blockName} block requires at least one identifier.");
        }

        return names;
    }

    public static Dictionary<string, string> ParseStringMapBlock(TokenReader reader, string endKind, string blockName)
    {
        BlockSyntax.BeginBlock(reader);
        reader.SkipNewlines();

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (!BlockSyntax.IsBlockEnd(reader, endKind) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, endKind))
            {
                break;
            }

            var key = reader.ReadIdent();
            reader.Expect(TokenKind.Eq);
            var value = reader.ReadString();
            if (!values.TryAdd(key, value))
            {
                throw new DashSpecParseException($"{blockName}: duplicate key '{key}'.");
            }

            reader.SkipNewlines();
        }

        BlockSyntax.ExpectBlockEnd(reader, endKind);
        if (values.Count == 0)
        {
            throw new DashSpecParseException($"{blockName} requires at least one entry.");
        }

        return values;
    }

    public static IReadOnlyList<string> ParseTitleListBlock(
        TokenReader reader,
        string endKind,
        string blockName)
    {
        BlockSyntax.BeginBlock(reader);
        reader.SkipNewlines();

        var titles = new List<string>();
        while (!BlockSyntax.IsBlockEnd(reader, endKind) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, endKind))
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
        BlockSyntax.ExpectBlockEnd(reader, endKind);
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

    public static IReadOnlyList<PropertySpec> CardLimits { get; } =
    [
        new("cells", PropertyValueType.Scalar),
        new("axis", PropertyValueType.Scalar),
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

    public static IReadOnlyList<PropertySpec> FilterBindDate { get; } =
    [
        new("column", PropertyValueType.ColumnBinding),
        new("default", PropertyValueType.DateRange),
        new("grain_filter", PropertyValueType.Scalar),
    ];

    public static IReadOnlyList<PropertySpec> FilterBindField { get; } =
    [
        new("column", PropertyValueType.ColumnBinding),
        new("default", PropertyValueType.Scalar),
        new("single", PropertyValueType.Scalar),
    ];

    public static IReadOnlyList<PropertySpec> FilterShow { get; } =
    [
        new("label", PropertyValueType.String),
        new("widget", PropertyValueType.Scalar),
        new("ref", PropertyValueType.Scalar),
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
        new("color_mode", PropertyValueType.Scalar),
        new("scale_value", PropertyValueType.Scalar),
        new("y_max", PropertyValueType.Scalar),
        new("default", PropertyValueType.Scalar),
        new("colors", PropertyValueType.Scalar),
    ];

    public static IReadOnlyList<PropertySpec> SeriesTransform { get; } =
    [
        new("use", PropertyValueType.Scalar),
        new("max", PropertyValueType.Scalar),
        new("other", PropertyValueType.String),
    ];

    public static IReadOnlyList<PropertySpec> Runtime { get; } =
    [
        new("manifest", PropertyValueType.String),
    ];

    public static IReadOnlyList<PropertySpec> Configuration { get; } =
    [
        new("sqldialect", PropertyValueType.Scalar),
        new("palette", PropertyValueType.String),
        new("diagramlibrary", PropertyValueType.String),
    ];

    public static IReadOnlyList<PropertySpec> Palette { get; } =
    [
        new("colors", PropertyValueType.String),
        new("default", PropertyValueType.String),
    ];
}
