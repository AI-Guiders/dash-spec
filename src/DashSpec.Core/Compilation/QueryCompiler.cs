using System.Text;
using DashSpec.Abstractions.Query;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;

namespace DashSpec.Core.Compilation;

public static class QueryCompiler
{
    public static CompiledQuery Compile(
        CardDefinition card,
        FilterState filters,
        IReadOnlyDictionary<string, FilterDefinition> filterDefinitions,
        SqlDialect sqlDialect = SqlDialect.TSql)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(filterDefinitions);

        var selectColumns = ResolveSelectColumns(card);
        var fromClause = card.DataSource.Kind switch
        {
            DataSourceKind.View => card.DataSource.Value,
            DataSourceKind.Sql => WrapSqlDataSource(card.DataSource.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(card)),
        };

        var parameters = new List<QueryParameter>();
        var whereBuilder = new StringBuilder("WHERE 1=1");

        AppendBoundFilters(whereBuilder, card, filters, filterDefinitions, parameters, sqlDialect);

        var sql = new StringBuilder();
        var tableLimit = DiagramKindRegistry.SupportsTopLimit(card.Diagram.Kind)
            ? ResolveTableLimit(card, filters, filterDefinitions)
            : 0;

        if (tableLimit > 0 && sqlDialect is SqlDialect.Postgres)
        {
            sql.Append("SELECT ").Append(selectColumns);
            sql.Append(" FROM ").Append(fromClause);
            sql.Append(' ').Append(whereBuilder);
            sql.Append(' ').Append(ResolveOrderBy(card));
            sql.Append(" LIMIT ").Append(tableLimit);
        }
        else
        {
            sql.Append("SELECT ");
            if (tableLimit > 0)
            {
                sql.Append("TOP ").Append(tableLimit).Append(' ');
            }

            sql.Append(selectColumns);
            sql.Append(" FROM ").Append(fromClause);
            sql.Append(' ').Append(whereBuilder);
            sql.Append(' ').Append(ResolveOrderBy(card));
        }

        return new CompiledQuery(sql.ToString(), parameters);
    }

    private static string WrapSqlDataSource(string rawSql)
    {
        SqlReadOnlyValidator.ValidateSqlBody(rawSql);
        return $"({rawSql.Trim().TrimEnd(';')}) AS _dashspec_q";
    }

    public static string BuildDistinctFieldSql(FilterDefinition filter)
    {
        if (filter.Kind is not FilterKind.Field || string.IsNullOrWhiteSpace(filter.ColumnReference))
        {
            throw new ArgumentException("Field filter requires column reference.", nameof(filter));
        }

        var lastDot = filter.ColumnReference.LastIndexOf('.');
        if (lastDot <= 0)
        {
            throw new ArgumentException($"Invalid column reference '{filter.ColumnReference}'.", nameof(filter));
        }

        var table = filter.ColumnReference[..lastDot];
        var column = filter.ColumnReference[(lastDot + 1)..];
        return $"SELECT DISTINCT {column} AS value FROM {table} WHERE {column} IS NOT NULL ORDER BY {column}";
    }

    private static string ResolveSelectColumns(CardDefinition card)
    {
        if (string.Equals(card.Diagram.Kind, "table", StringComparison.OrdinalIgnoreCase))
        {
            if (card.Diagram.Properties.TryGetValue("columns", out var columns))
            {
                return columns;
            }

            return "*";
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in new[] { "x", "y", "series", "value", "tooltip" })
        {
            if (card.Diagram.Properties.TryGetValue(key, out var value))
            {
                names.Add(value);
            }
        }

        if (names.Count == 0)
        {
            return "*";
        }

        return string.Join(", ", names.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
    }

    private static string ResolveOrderBy(CardDefinition card)
    {
        if (card.Diagram.Properties.TryGetValue("order_by", out var orderBy))
        {
            return $"ORDER BY {orderBy}";
        }

        if (card.Diagram.Properties.TryGetValue("x", out var x))
        {
            return $"ORDER BY {x}";
        }

        return string.Empty;
    }

    private static int ResolveTableLimit(
        CardDefinition card,
        FilterState filters,
        IReadOnlyDictionary<string, FilterDefinition> filterDefinitions)
    {
        foreach (var filterName in card.BoundFilters)
        {
            if (!filterDefinitions.TryGetValue(filterName, out var definition) ||
                definition.Kind is not FilterKind.Top)
            {
                continue;
            }

            return TopLimitDefaults.Resolve(definition, filters.GetTop(filterName));
        }

        if (card.Diagram.Properties.TryGetValue("limit", out var raw) &&
            int.TryParse(raw, out var limit) &&
            limit is > 0 and <= TopLimitDefaults.DefaultMax)
        {
            return limit;
        }

        return 500;
    }

    private static void AppendBoundFilters(
        StringBuilder whereBuilder,
        CardDefinition card,
        FilterState filters,
        IReadOnlyDictionary<string, FilterDefinition> filterDefinitions,
        List<QueryParameter> parameters,
        SqlDialect sqlDialect)
    {
        foreach (var filterName in card.BoundFilters)
        {
            if (!filterDefinitions.TryGetValue(filterName, out var definition) ||
                definition.Kind is not FilterKind.Date and not FilterKind.Field)
            {
                continue;
            }

            var clause = BuildClause(definition, filters, parameters, sqlDialect);
            if (clause is null)
            {
                continue;
            }

            whereBuilder.Append(" AND ").Append(clause);
        }
    }

    private static string? BuildClause(
        FilterDefinition definition,
        FilterState filters,
        List<QueryParameter> parameters,
        SqlDialect sqlDialect)
    {
        return definition.Kind switch
        {
            FilterKind.Date => BuildDateClause(filters.GetDate(definition.Name), definition, parameters, sqlDialect),
            FilterKind.Field => BuildFieldClause(filters.GetField(definition.Name), definition, parameters),
            _ => null,
        };
    }

    private static string ResolveColumnName(string? columnReference, string fallback)
    {
        if (string.IsNullOrWhiteSpace(columnReference))
        {
            return fallback;
        }

        var lastDot = columnReference.LastIndexOf('.');
        return lastDot >= 0 ? columnReference[(lastDot + 1)..] : columnReference;
    }

    private static string? BuildDateClause(
        DateRangeValue? range,
        FilterDefinition definition,
        List<QueryParameter> parameters,
        SqlDialect sqlDialect)
    {
        if (range is null)
        {
            return null;
        }

        var variable = definition.Name;
        var column = ResolveColumnName(definition.ColumnReference, variable);
        var fromParam = $"@{variable}_from";
        var toParam = $"@{variable}_to";
        parameters.Add(new QueryParameter(fromParam, range.Value.From));
        parameters.Add(new QueryParameter(toParam, range.Value.To));

        var upperExclusive = sqlDialect switch
        {
            SqlDialect.Postgres => $"({toParam}::date + INTERVAL '1 day')",
            SqlDialect.TSql or SqlDialect.Generic => $"DATEADD(day, 1, {toParam})",
            _ => $"DATEADD(day, 1, {toParam})",
        };

        return $"{column} >= {fromParam} AND {column} < {upperExclusive}";
    }

    private static string? BuildFieldClause(
        FieldFilterValue? field,
        FilterDefinition definition,
        List<QueryParameter> parameters)
    {
        if (field is null || !field.Value.HasSelection)
        {
            return null;
        }

        var variable = definition.Name;
        var column = ResolveColumnName(definition.ColumnReference, variable);

        if (field.Value.Values.Count == 1)
        {
            var param = $"@{variable}_0";
            parameters.Add(new QueryParameter(param, field.Value.Values[0]));
            return $"{column} = {param}";
        }

        var placeholders = new List<string>();
        for (var i = 0; i < field.Value.Values.Count; i++)
        {
            var param = $"@{variable}_{i}";
            placeholders.Add(param);
            parameters.Add(new QueryParameter(param, field.Value.Values[i]));
        }

        return $"{column} IN ({string.Join(", ", placeholders)})";
    }
}
