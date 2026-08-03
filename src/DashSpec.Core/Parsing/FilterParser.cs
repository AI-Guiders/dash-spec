using DashSpec.Core.Model;
using DashSpec.Core.Runtime;

namespace DashSpec.Core.Parsing;

/// <summary>
/// Filter declaration grammar (ADR-0010 legacy kind-first; ADR-0037 structured id-first bind/show).
/// Authoring reference: <see cref="Authoring.AuthoringCatalog.Filters"/>.
/// </summary>
internal static class FilterParser
{
    public static FilterDefinition Parse(TokenReader reader)
    {
        if (!reader.TryPeekIdent(out var first))
        {
            throw reader.Unexpected("filter id or kind (date, field, top)");
        }

        if (IsFilterKind(first))
        {
            return ParseLegacyKindFirst(reader, ParseFilterKindFromIdent(reader.ReadIdent()));
        }

        var name = reader.ReadIdentSameLine();
        return ParseStructuredIdFirst(reader, name);
    }

    private static bool IsFilterKind(string value) =>
        value.Equals("date", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("field", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("top", StringComparison.OrdinalIgnoreCase);

    private static FilterKind ParseFilterKindFromIdent(string ident) =>
        ident.ToLowerInvariant() switch
        {
            "date" => FilterKind.Date,
            "field" => FilterKind.Field,
            "top" => FilterKind.Top,
            _ => throw new DashSpecParseException($"Unknown filter kind '{ident}'."),
        };

    private static FilterDefinition ParseStructuredIdFirst(TokenReader reader, string name)
    {
        FilterKind? kind = null;
        Dictionary<string, string> bindProps = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> showProps = new(StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, string>? grainLabels = null;

        BlockSyntax.BeginBlock(reader);
        reader.SkipNewlines();

        while (!BlockSyntax.IsBlockEnd(reader, "filter") && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, "filter"))
            {
                break;
            }

            if (reader.TryKeyword("bind"))
            {
                if (kind is not null)
                {
                    throw new DashSpecParseException($"Filter '{name}': duplicate bind block.");
                }

                kind = ParseBindKind(reader, name);
                var parsed = ParseStructuredBindBlock(reader, kind.Value, name);
                bindProps = parsed.Properties;
                grainLabels = parsed.GrainLabels;
                continue;
            }

            if (reader.TryKeyword("show"))
            {
                if (showProps.Count > 0)
                {
                    throw new DashSpecParseException($"Filter '{name}': duplicate show block.");
                }

                showProps = PropertyBlockParser.Parse(
                    reader,
                    PropertySchemas.FilterShow,
                    $"filter {name} show",
                    endKind: "show");
                continue;
            }

            throw reader.Unexpected("bind or show");
        }

        BlockSyntax.ExpectBlockEnd(reader, "filter");

        if (kind is null)
        {
            throw new DashSpecParseException($"Filter '{name}': bind block is required.");
        }

        bindProps.TryGetValue("default", out var defaultExpression);
        bindProps.TryGetValue("column", out var columnReference);
        showProps.TryGetValue("widget", out var widget);
        showProps.TryGetValue("grain_filter", out var grainFilterName);
        bindProps.TryGetValue("grain_filter", out var grainFromBind);
        grainFilterName ??= grainFromBind;
        showProps.TryGetValue("ref", out var layoutRef);

        var label = ResolveStructuredLabel(name, kind.Value, showProps);
        var singleSelect = ResolveSingleSelect(widget, bindProps);
        int? minValue = null;
        int? maxValue = null;

        ValidateSemantics(
            kind.Value,
            name,
            ref defaultExpression,
            widget,
            columnReference,
            bindProps,
            ref minValue,
            ref maxValue,
            singleSelect);

        return new FilterDefinition(
            kind.Value,
            name,
            defaultExpression,
            columnReference,
            label,
            widget,
            minValue,
            maxValue,
            grainFilterName,
            singleSelect,
            layoutRef,
            grainLabels);
    }

    private static FilterKind ParseBindKind(TokenReader reader, string filterName)
    {
        if (!reader.TryPeekIdent(out var kindIdent))
        {
            throw reader.Unexpected("bind kind (date, field, or top)");
        }

        if (!IsFilterKind(kindIdent))
        {
            throw new DashSpecParseException(
                $"Filter '{filterName}': bind requires date, field, or top, got '{kindIdent}'.");
        }

        return ParseFilterKindFromIdent(reader.ReadIdent());
    }

    private sealed record StructuredBindParse(
        Dictionary<string, string> Properties,
        IReadOnlyDictionary<string, string>? GrainLabels);

    private static StructuredBindParse ParseStructuredBindBlock(
        TokenReader reader,
        FilterKind kind,
        string name)
    {
        var schema = kind switch
        {
            FilterKind.Date => PropertySchemas.FilterBindDate,
            FilterKind.Field => PropertySchemas.FilterBindField,
            FilterKind.Top => PropertySchemas.FilterTop,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        var blockName = $"filter {name} bind {kind.ToString().ToLowerInvariant()}";
        BlockSyntax.BeginBlock(reader);
        reader.SkipNewlines();

        var specs = schema.ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, string>? grainLabels = null;

        while (!BlockSyntax.IsBlockEnd(reader, "bind") && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, "bind"))
            {
                break;
            }

            while (!reader.IsOnNewline() &&
                   !BlockSyntax.IsBlockEnd(reader, "bind") &&
                   !reader.IsEof)
            {
                if (BlockSyntax.IsBlockEnd(reader, "bind"))
                {
                    break;
                }

                var key = reader.ReadPropertyKey(allowQuoted: false);
                if (string.Equals(key, "labels", StringComparison.OrdinalIgnoreCase))
                {
                    if (kind is not FilterKind.Date)
                    {
                        throw new DashSpecParseException(
                            $"{blockName}: labels block is allowed only on date filters.");
                    }

                    if (grainLabels is not null)
                    {
                        throw new DashSpecParseException($"{blockName}: duplicate labels block.");
                    }

                    reader.SkipNewlines();
                    grainLabels = PropertyBlockParser.ParseStringMapBlock(reader, "labels", $"{blockName} labels");
                    continue;
                }

                if (!specs.TryGetValue(key, out var spec))
                {
                    throw new DashSpecParseException($"Unknown property '{key}' in {blockName} block.");
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

        BlockSyntax.ExpectBlockEnd(reader, "bind");
        return new StructuredBindParse(values, grainLabels);
    }

    private static string ResolveStructuredLabel(
        string name,
        FilterKind kind,
        IReadOnlyDictionary<string, string> showProps)
    {
        if (kind is FilterKind.Top or FilterKind.Date or FilterKind.Field)
        {
            if (!showProps.TryGetValue("label", out var label) || string.IsNullOrWhiteSpace(label))
            {
                throw new DashSpecParseException($"Filter '{name}': show block requires label = \"…\".");
            }

            return label;
        }

        throw new ArgumentOutOfRangeException(nameof(kind));
    }

    private static FilterDefinition ParseLegacyKindFirst(TokenReader reader, FilterKind kind)
    {
        var name = reader.ReadIdentSameLine();
        var declarationLabel = TryParseTopLabel(reader, kind);
        var (columnFromOn, labelFromOn) = TryParseOnBinding(reader, kind);
        var trailingLabel = labelFromOn is null && kind is FilterKind.Date or FilterKind.Field && reader.TryKeywordSameLine("as")
            ? reader.ReadString()
            : null;
        var trailingDefault = TryParseTrailingDateDefault(reader, kind);
        var layoutRef = ParserUtilities.TryReadLayoutRef(reader);

        var props = ParseFilterBody(reader, kind, name, columnFromOn is not null, out var grainLabels);

        var columnReference = columnFromOn;
        if (columnReference is null)
        {
            props.TryGetValue("column", out columnReference);
        }

        props.TryGetValue("default", out var defaultExpression);
        defaultExpression ??= trailingDefault;
        var label = ResolveFilterLabel(name, kind, props, declarationLabel, labelFromOn ?? trailingLabel);
        props.TryGetValue("widget", out var widget);
        props.TryGetValue("grain_filter", out var grainFilterName);
        int? minValue = null;
        int? maxValue = null;

        var singleSelect = ResolveSingleSelect(widget, props);
        ValidateSemantics(kind, name, ref defaultExpression, widget, columnReference, props, ref minValue, ref maxValue, singleSelect);

        return new FilterDefinition(
            kind,
            name,
            defaultExpression,
            columnReference,
            label,
            widget,
            minValue,
            maxValue,
            grainFilterName,
            singleSelect,
            layoutRef,
            grainLabels);
    }

    private static bool ResolveSingleSelect(string? widget, IReadOnlyDictionary<string, string> props)
    {
        if (string.Equals(widget, "select", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return props.TryGetValue("single", out var raw) &&
               (string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase));
    }

    private static string? TryParseTopLabel(TokenReader reader, FilterKind kind)
    {
        if (kind is not FilterKind.Top || !reader.TryKeyword("as"))
        {
            return null;
        }

        return reader.ReadString();
    }

    private static (string? Column, string? Label) TryParseOnBinding(TokenReader reader, FilterKind kind)
    {
        if (kind is not (FilterKind.Date or FilterKind.Field) || !reader.TryKeywordSameLine("on"))
        {
            return (null, null);
        }

        var binding = reader.ReadColumnBinding();
        return (binding.Column, binding.Alias);
    }

    private static string? TryParseTrailingDateDefault(TokenReader reader, FilterKind kind)
    {
        if (kind is not FilterKind.Date || reader.IsOnNewline() || reader.IsEof)
        {
            return null;
        }

        if (reader.TryPeekIdent(out var next) &&
            (string.Equals(next, "default", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(next, "widget", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(next, "ref", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(next, "grain_filter", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (reader.RawKind is not (TokenKind.RelativeDay or TokenKind.Ident))
        {
            return null;
        }

        return reader.ReadDateDefaultValue();
    }

    private static Dictionary<string, string> ParseFilterBody(
        TokenReader reader,
        FilterKind kind,
        string name,
        bool columnProvidedByOn,
        out IReadOnlyDictionary<string, string>? grainLabels)
    {
        grainLabels = null;
        if (reader.RawKind is TokenKind.LBrace)
        {
            throw new DashSpecParseException(
                $"Filter '{name}': brace bodies removed; use properties + end filter.");
        }

        if (HasInlineProperties(reader))
        {
            var props = ParseInlineProperties(reader, kind);
            reader.SkipNewlines();
            if (!reader.IsEof && ShouldStartLegacyPropertyBlock(reader))
            {
                var blockProps = ParsePropertyBlock(reader, kind, name, columnProvidedByOn, out grainLabels);
                foreach (var (key, value) in blockProps)
                {
                    props[key] = value;
                }
            }

            return props;
        }

        reader.SkipNewlines();
        if (!reader.IsEof && ShouldStartLegacyPropertyBlock(reader))
        {
            return ParsePropertyBlock(reader, kind, name, columnProvidedByOn, out grainLabels);
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static bool ShouldStartLegacyPropertyBlock(TokenReader reader) =>
        LooksLikeFilterPropertyContinuation(reader);

    private static bool LooksLikeFilterPropertyContinuation(TokenReader reader)
    {
        reader.SkipNewlines();
        if (reader.IsEof || BlockSyntax.IsBlockEnd(reader, "filter"))
        {
            return false;
        }

        if (reader.RawKind is not TokenKind.Ident)
        {
            return false;
        }

        if (!reader.TryPeekIdent(out var key))
        {
            return false;
        }

        if (string.Equals(key, "end", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return key.Equals("column", StringComparison.OrdinalIgnoreCase)
            || key.Equals("default", StringComparison.OrdinalIgnoreCase)
            || key.Equals("widget", StringComparison.OrdinalIgnoreCase)
            || key.Equals("grain_filter", StringComparison.OrdinalIgnoreCase)
            || key.Equals("single", StringComparison.OrdinalIgnoreCase)
            || key.Equals("labels", StringComparison.OrdinalIgnoreCase)
            || key.Equals("min", StringComparison.OrdinalIgnoreCase)
            || key.Equals("max", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasInlineProperties(TokenReader reader)
    {
        if (reader.IsOnNewline())
        {
            return false;
        }

        return reader.RawKind switch
        {
            TokenKind.Eof or TokenKind.RBrace or TokenKind.LBrace => false,
            TokenKind.Ident => true,
            _ => false,
        };
    }

    private static Dictionary<string, string> ParseInlineProperties(TokenReader reader, FilterKind kind)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (HasInlineProperties(reader))
        {
            var key = reader.ReadIdent();
            if (string.Equals(key, "single", StringComparison.OrdinalIgnoreCase) &&
                reader.RawKind is not TokenKind.Eq)
            {
                props[key] = "true";
                continue;
            }

            if (reader.RawKind is TokenKind.Eq)
            {
                reader.Advance();
            }

            var value = key.Equals("default", StringComparison.OrdinalIgnoreCase) && kind is FilterKind.Date
                ? reader.ReadDateDefaultValue()
                : reader.ReadScalarValue();
            props[key] = value;
        }

        reader.SkipNewlines();
        return props;
    }

    private static Dictionary<string, string> ParsePropertyBlock(
        TokenReader reader,
        FilterKind kind,
        string name,
        bool columnProvidedByOn,
        out IReadOnlyDictionary<string, string>? grainLabels)
    {
        grainLabels = null;
        var schema = kind switch
        {
            FilterKind.Date => PropertySchemas.FilterDate,
            FilterKind.Field => PropertySchemas.FilterField,
            FilterKind.Top => PropertySchemas.FilterTop,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        var blockName = $"filter {kind} {name}";
        BlockSyntax.BeginBlock(reader);
        reader.SkipNewlines();

        var specs = schema.ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (!BlockSyntax.IsBlockEnd(reader, "filter") && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (BlockSyntax.IsBlockEnd(reader, "filter"))
            {
                break;
            }

            while (!reader.IsAt(TokenKind.Newline) &&
                   !BlockSyntax.IsBlockEnd(reader, "filter") &&
                   !reader.IsEof)
            {
                var key = reader.ReadPropertyKey(allowQuoted: false);
                if (string.Equals(key, "labels", StringComparison.OrdinalIgnoreCase))
                {
                    if (kind is not FilterKind.Date)
                    {
                        throw new DashSpecParseException(
                            $"{blockName}: labels block is allowed only on date filters.");
                    }

                    if (grainLabels is not null)
                    {
                        throw new DashSpecParseException($"{blockName}: duplicate labels block.");
                    }

                    reader.SkipNewlines();
                    grainLabels = PropertyBlockParser.ParseStringMapBlock(reader, "labels", $"{blockName} labels");
                    continue;
                }

                if (!specs.TryGetValue(key, out var spec))
                {
                    throw new DashSpecParseException($"Unknown property '{key}' in {blockName} block.");
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

        BlockSyntax.ExpectBlockEnd(reader, "filter");
        if (columnProvidedByOn)
        {
            values.Remove("column");
            values.Remove("column_as");
        }

        return values;
    }

    private static string ReadTypedValue(TokenReader reader, PropertyValueType type) =>
        type switch
        {
            PropertyValueType.Scalar => reader.ReadScalarValue(),
            PropertyValueType.String => reader.ReadString(),
            PropertyValueType.DateRange => reader.ReadDateDefaultValue(),
            PropertyValueType.QualifiedName => reader.ReadQualifiedName(),
            PropertyValueType.CommaList => reader.ReadCommaSeparatedValues(),
            PropertyValueType.RestOfLine => reader.ReadRestOfLine(),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

    private static string ResolveFilterLabel(
        string name,
        FilterKind kind,
        IReadOnlyDictionary<string, string> props,
        string? declarationLabel,
        string? labelFromOn)
    {
        if (kind is FilterKind.Top)
        {
            if (string.IsNullOrWhiteSpace(declarationLabel))
            {
                throw new DashSpecParseException($"Top filter '{name}' requires as \"Label\".");
            }

            return declarationLabel;
        }

        if (!string.IsNullOrWhiteSpace(labelFromOn))
        {
            return labelFromOn;
        }

        if (!string.IsNullOrWhiteSpace(declarationLabel))
        {
            throw new DashSpecParseException(
                $"Filter '{name}' uses on <column> as \"Label\", not as on the filter line.");
        }

        if (props.TryGetValue("column_as", out var columnLabel) && !string.IsNullOrWhiteSpace(columnLabel))
        {
            return columnLabel;
        }

        throw new DashSpecParseException(
            $"Filter '{name}' requires on <column> as \"Label\" or column = … as \"Label\" in {{ }}.");
    }

    private static void ValidateSemantics(
        FilterKind kind,
        string name,
        ref string? defaultExpression,
        string? widget,
        string? columnReference,
        IReadOnlyDictionary<string, string> props,
        ref int? minValue,
        ref int? maxValue,
        bool singleSelect)
    {
        if (kind is FilterKind.Date)
        {
            if (string.IsNullOrWhiteSpace(defaultExpression))
            {
                throw new DashSpecParseException(
                    $"Date filter '{name}' requires default range, e.g. default = -7d..today");
            }

            if (string.Equals(widget, "day", StringComparison.OrdinalIgnoreCase) &&
                !defaultExpression.Contains("..", StringComparison.Ordinal))
            {
                defaultExpression = $"{defaultExpression}..{defaultExpression}";
            }
            else if (!defaultExpression.Contains("..", StringComparison.Ordinal))
            {
                throw new DashSpecParseException(
                    $"Date filter '{name}' default must be a range 'from..to', e.g. -7d..today");
            }

            DateDefaultRange.ValidateSyntax(defaultExpression);
            if (string.Equals(widget, "day", StringComparison.OrdinalIgnoreCase))
            {
                DateDefaultRange.ValidateSingleDayDefault(defaultExpression);
            }
            else if (!string.IsNullOrWhiteSpace(widget) &&
                     !string.Equals(widget, "range", StringComparison.OrdinalIgnoreCase))
            {
                throw new DashSpecParseException(
                    $"Date filter '{name}' widget must be 'day' or 'range', got '{widget}'.");
            }

            if (string.IsNullOrWhiteSpace(columnReference))
            {
                throw new DashSpecParseException(
                    $"Date filter '{name}' requires column in bind block or on <column> as \"Label\".");
            }
        }
        else if (kind is FilterKind.Field)
        {
            if (!string.IsNullOrWhiteSpace(widget) &&
                !string.Equals(widget, "combobox", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(widget, "chips", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(widget, "select", StringComparison.OrdinalIgnoreCase))
            {
                throw new DashSpecParseException(
                    $"Field filter '{name}' widget must be 'combobox', 'chips', or 'select', got '{widget}'.");
            }

            if (singleSelect &&
                !string.IsNullOrWhiteSpace(defaultExpression) &&
                defaultExpression.Contains(',', StringComparison.Ordinal))
            {
                throw new DashSpecParseException(
                    $"Field filter '{name}' is single-select; default must be one value, got '{defaultExpression}'.");
            }

            if (string.IsNullOrWhiteSpace(columnReference))
            {
                throw new DashSpecParseException(
                    $"Field filter '{name}' requires column in bind block or on <column> as \"Label\".");
            }
        }
        else if (kind is FilterKind.Top)
        {
            if (string.IsNullOrWhiteSpace(defaultExpression) ||
                !int.TryParse(defaultExpression, out var defaultTop) ||
                defaultTop <= 0)
            {
                throw new DashSpecParseException(
                    $"Top filter '{name}' requires positive numeric default, e.g. default = 200");
            }

            if (props.TryGetValue("min", out var minRaw))
            {
                if (!int.TryParse(minRaw, out var parsedMin) || parsedMin <= 0)
                {
                    throw new DashSpecParseException($"Top filter '{name}' min must be a positive integer.");
                }

                minValue = parsedMin;
            }

            if (props.TryGetValue("max", out var maxRaw))
            {
                if (!int.TryParse(maxRaw, out var parsedMax) || parsedMax <= 0)
                {
                    throw new DashSpecParseException($"Top filter '{name}' max must be a positive integer.");
                }

                maxValue = parsedMax;
            }

            if (minValue is > 0 && maxValue is > 0 && minValue > maxValue)
            {
                throw new DashSpecParseException($"Top filter '{name}' min cannot exceed max.");
            }
        }
    }
}
