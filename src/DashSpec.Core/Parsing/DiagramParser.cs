using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class DiagramParser
{
    public static DiagramDefinition Parse(TokenReader reader)
    {
        var name = reader.ReadIdent();
        if (DiagramKindRegistry.TryResolve(name, out var spec))
        {
            var properties = PropertyBlockParser.Parse(
                reader,
                DiagramKindRegistry.GetProperties(name),
                $"diagram {name}",
                spec.AllowExtensionProperties);
            return new DiagramDefinition(name, properties);
        }

        var overrides = reader.IsAt(TokenKind.LBrace)
            ? PropertyBlockParser.Parse(
                reader,
                DiagramKindRegistry.AllBindingProperties(),
                $"diagram {name}",
                allowExtensionProperties: true)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return new DiagramDefinition(string.Empty, overrides, name);
    }
}

internal static class DataSourceParser
{
    public static DataSourceDefinition Parse(TokenReader reader)
    {
        if (reader.TryKeyword("view"))
        {
            var name = reader.ReadQualifiedName();
            SqlReadOnlyValidator.ValidateViewReference(name);
            return new DataSourceDefinition(DataSourceKind.View, name);
        }

        if (reader.TryKeyword("sql"))
        {
            var body = reader.ReadString();
            SqlReadOnlyValidator.ValidateSqlBody(body);
            return new DataSourceDefinition(DataSourceKind.Sql, body);
        }

        throw reader.Unexpected("view or sql");
    }
}

internal static class LayoutParser
{
    public static LayoutDefinition ParseGrid(TokenReader reader)
    {
        _ = reader.ReadIdent() switch
        {
            "grid" => true,
            _ => throw reader.Unexpected("grid"),
        };

        var props = PropertyBlockParser.Parse(reader, PropertySchemas.LayoutGrid, "layout grid");

        var columns = LayoutDefinition.Default.Columns;
        var gap = LayoutDefinition.Default.GapPx;

        if (props.TryGetValue("columns", out var columnsRaw) &&
            int.TryParse(columnsRaw, out var parsedColumns) &&
            parsedColumns is > 0 and <= 24)
        {
            columns = parsedColumns;
        }

        if (props.TryGetValue("gap", out var gapRaw) &&
            int.TryParse(gapRaw, out var parsedGap) &&
            parsedGap >= 0)
        {
            gap = parsedGap;
        }

        return new LayoutDefinition(columns, gap);
    }

    public static PlacementDefinition ParsePlacement(TokenReader reader)
    {
        var props = PropertyBlockParser.Parse(reader, PropertySchemas.Placement, "place");

        var row = 1;
        var col = 1;
        var span = 6;

        if (props.TryGetValue("row", out var rowRaw) &&
            int.TryParse(rowRaw, out var parsedRow) &&
            parsedRow > 0)
        {
            row = parsedRow;
        }

        if (props.TryGetValue("col", out var colRaw) &&
            int.TryParse(colRaw, out var parsedCol) &&
            parsedCol > 0)
        {
            col = parsedCol;
        }

        if (props.TryGetValue("span", out var spanRaw))
        {
            span = ParseSpanValue(spanRaw);
        }

        return new PlacementDefinition(row, col, span);
    }

    private static int ParseSpanValue(string value) =>
        value.ToLowerInvariant() switch
        {
            "full" => 12,
            "half" => 6,
            "third" => 4,
            _ when int.TryParse(value, out var parsed) && parsed > 0 => parsed,
            _ => 6,
        };
}
