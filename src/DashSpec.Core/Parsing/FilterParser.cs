using DashSpec.Core.Model;
using DashSpec.Core.Runtime;

namespace DashSpec.Core.Parsing;

/// <summary>
/// Filter declaration grammar (ADR-0010):
/// <code>
/// filterDecl   ::= 'filter' filterKind ident topLabel? onBinding? filterBody
/// filterKind   ::= 'date' | 'field' | 'top'
/// topLabel     ::= 'as' string
/// onBinding    ::= 'on' columnBinding
/// filterBody   ::= propertyBlock | inlineProperties | ε
/// propertyBlock ::= '{' property* '}'
/// inlineProperties ::= (ident ('=' value)?)+   // same physical line only
/// </code>
/// </summary>
internal static class FilterParser
{
    public static FilterDefinition Parse(TokenReader reader)
    {
        var kind = ParseFilterKind(reader);
        var name = reader.ReadIdent();
        var declarationLabel = TryParseTopLabel(reader, kind);
        var (columnFromOn, labelFromOn) = TryParseOnBinding(reader, kind);
        var trailingLabel = labelFromOn is null && kind is FilterKind.Date or FilterKind.Field && reader.TryKeywordSameLine("as")
            ? reader.ReadString()
            : null;
        var layoutRef = ParserUtilities.TryReadLayoutRef(reader);

        var props = ParseFilterBody(reader, kind, name, columnFromOn is not null);

        var columnReference = columnFromOn;
        if (columnReference is null)
        {
            props.TryGetValue("column", out columnReference);
        }

        props.TryGetValue("default", out var defaultExpression);
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
            layoutRef);
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

    private static FilterKind ParseFilterKind(TokenReader reader) =>
        reader.ReadIdent() switch
        {
            "date" => FilterKind.Date,
            "field" => FilterKind.Field,
            "top" => FilterKind.Top,
            _ => throw reader.Unexpected("date, field, or top"),
        };

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
        if (kind is not (FilterKind.Date or FilterKind.Field) || !reader.TryKeyword("on"))
        {
            return (null, null);
        }

        var binding = reader.ReadColumnBinding();
        return (binding.Column, binding.Alias);
    }

    private static Dictionary<string, string> ParseFilterBody(
        TokenReader reader,
        FilterKind kind,
        string name,
        bool columnProvidedByOn)
    {
        if (reader.RawKind is TokenKind.LBrace)
        {
            return ParsePropertyBlock(reader, kind, name, columnProvidedByOn);
        }

        if (HasInlineProperties(reader))
        {
            return ParseInlineProperties(reader, kind);
        }

        if (reader.IsOnNewline())
        {
            reader.SkipNewlines();
            if (reader.IsAt(TokenKind.LBrace))
            {
                return ParsePropertyBlock(reader, kind, name, columnProvidedByOn);
            }
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
        bool columnProvidedByOn)
    {
        var schema = kind switch
        {
            FilterKind.Date => PropertySchemas.FilterDate,
            FilterKind.Field => PropertySchemas.FilterField,
            FilterKind.Top => PropertySchemas.FilterTop,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        var blockName = $"filter {kind} {name}";
        var props = PropertyBlockParser.Parse(reader, schema, blockName);
        if (columnProvidedByOn)
        {
            props.Remove("column");
            props.Remove("column_as");
        }

        return props;
    }

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
                    $"Date filter '{name}' requires on <column> as \"Label\" or column = … in {{ }}.");
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
                    $"Field filter '{name}' requires on <column> as \"Label\" or column = … in {{ }}.");
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
