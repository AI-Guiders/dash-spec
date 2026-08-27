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
        SqlDialect sqlDialect = SqlDialect.TSql,
        string? specDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(filterDefinitions);

        var fromClause = card.DataSource.Kind switch
        {
            DataSourceKind.View => card.DataSource.Value,
            DataSourceKind.Sql => WrapSqlDataSource(
                SqlDataSourceResolver.ResolveSqlBody(card.DataSource, specDirectory)),
            _ => throw new ArgumentOutOfRangeException(nameof(card)),
        };

        var parameters = new List<QueryParameter>();
        var whereBuilder = new StringBuilder("WHERE 1=1");
        AppendBoundFilters(whereBuilder, card, filters, filterDefinitions, parameters, sqlDialect);

        var tableLimit = DiagramKindRegistry.SupportsTopLimit(card.Diagram.Kind)
            ? ResolveTableLimit(card, filters, filterDefinitions)
            : 0;

        if (TryBuildScalarAggregateSelect(card.Diagram, out var scalarSelect))
        {
            return new CompiledQuery(
                BuildSelectSql(
                    scalarSelect,
                    fromClause,
                    whereBuilder.ToString(),
                    groupBy: null,
                    orderBy: string.Empty,
                    tableLimit,
                    sqlDialect),
                parameters);
        }

        if (TryBuildCategoryAggregateSelect(card.Diagram, out var aggregateSelect, out var groupBy))
        {
            aggregateSelect = AppendOrderByAggregates(aggregateSelect, card.Diagram, groupBy);
            return new CompiledQuery(
                BuildSelectSql(
                    aggregateSelect,
                    fromClause,
                    whereBuilder.ToString(),
                    groupBy,
                    ResolveOrderBy(card),
                    tableLimit,
                    sqlDialect),
                parameters);
        }

        return new CompiledQuery(
            BuildSelectSql(
                ResolveSelectColumns(card),
                fromClause,
                whereBuilder.ToString(),
                groupBy: null,
                ResolveOrderBy(card),
                tableLimit,
                sqlDialect),
            parameters);
    }

    private static string BuildSelectSql(
        string selectList,
        string fromClause,
        string whereClause,
        string? groupBy,
        string orderBy,
        int tableLimit,
        SqlDialect sqlDialect)
    {
        var sql = new StringBuilder();
        if (tableLimit > 0 && sqlDialect is SqlDialect.Postgres)
        {
            sql.Append("SELECT ").Append(selectList);
            sql.Append(" FROM ").Append(fromClause);
            sql.Append(' ').Append(whereClause);
            if (!string.IsNullOrEmpty(groupBy))
            {
                sql.Append(' ').Append(groupBy);
            }

            sql.Append(' ').Append(orderBy);
            sql.Append(" LIMIT ").Append(tableLimit);
            return sql.ToString();
        }

        sql.Append("SELECT ");
        if (tableLimit > 0)
        {
            sql.Append("TOP ").Append(tableLimit).Append(' ');
        }

        sql.Append(selectList);
        sql.Append(" FROM ").Append(fromClause);
        sql.Append(' ').Append(whereClause);
        if (!string.IsNullOrEmpty(groupBy))
        {
            sql.Append(' ').Append(groupBy);
        }

        sql.Append(' ').Append(orderBy);
        return sql.ToString();
    }

    /// <summary>
    /// Scalar KPI (<c>number</c>): filter first, then <c>SUM|MAX|MIN|AVG(value)</c>
    /// (no GROUP BY) so day-grain views roll up over the bound period.
    /// <c>aggregate = none</c> keeps row-level select (first-row display).
    /// </summary>
    private static bool TryBuildScalarAggregateSelect(DiagramDefinition diagram, out string selectList)
    {
        selectList = string.Empty;

        if (!string.Equals(diagram.Kind, "number", StringComparison.OrdinalIgnoreCase) ||
            !DiagramBindings.TryGetColumn(diagram, "value", out var measure))
        {
            return false;
        }

        if (!TryResolveScalarAggregate(diagram, out var aggregate))
        {
            return false;
        }

        selectList = aggregate is "COUNT"
            ? $"COUNT({measure}) AS {measure}"
            : $"{aggregate}({measure}) AS {measure}";
        return true;
    }

    /// <summary>
    /// Returns false when aggregation is explicitly disabled (<c>none</c>/<c>raw</c>).
    /// </summary>
    private static bool TryResolveScalarAggregate(DiagramDefinition diagram, out string sqlFn)
    {
        sqlFn = "SUM";
        if (!diagram.Properties.TryGetValue("aggregate", out var raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        switch (raw.Trim().ToLowerInvariant())
        {
            case "sum":
                sqlFn = "SUM";
                return true;
            case "max":
                sqlFn = "MAX";
                return true;
            case "min":
                sqlFn = "MIN";
                return true;
            case "avg":
            case "average":
            case "mean":
                sqlFn = "AVG";
                return true;
            case "count":
                sqlFn = "COUNT";
                return true;
            case "none":
            case "raw":
            case "first":
                return false;
            default:
                throw new InvalidOperationException(
                    $"Diagram number aggregate '{raw}' is not supported (sum|max|min|avg|count|none).");
        }
    }

    /// <summary>
    /// Category charts (bar/pie/donut without series/x_step): filter first, then
    /// <c>SUM(measure) GROUP BY category</c> so day-grain views roll up over the bound period.
    /// </summary>
    private static bool TryBuildCategoryAggregateSelect(
        DiagramDefinition diagram,
        out string selectList,
        out string groupBy)
    {
        selectList = string.Empty;
        groupBy = string.Empty;

        if (!ShouldAggregateCategoryTotals(diagram) ||
            !DiagramBindings.TryGetColumn(diagram, "x", out var category) ||
            !DiagramBindings.TryGetColumn(diagram, "y", out var measure))
        {
            return false;
        }

        var parts = new List<string>
        {
            category,
            $"SUM({measure}) AS {measure}",
        };
        var groupParts = new List<string> { category };

        if (DiagramBindings.TryGetColumn(diagram, "reference", out var reference))
        {
            // Purchased seats / caps are not additive across days.
            parts.Add($"MAX({reference}) AS {reference}");
        }

        selectList = string.Join(", ", parts);
        groupBy = "GROUP BY " + string.Join(", ", groupParts);
        return true;
    }

    /// <summary>
    /// Keep ORDER BY columns available after GROUP BY via MAX(col) AS col
    /// (e.g. utilization_pct on stakeholder bars).
    /// </summary>
    private static string AppendOrderByAggregates(
        string selectList,
        DiagramDefinition diagram,
        string groupBy)
    {
        if (!diagram.Properties.TryGetValue("order_by", out var orderBy) ||
            string.IsNullOrWhiteSpace(orderBy))
        {
            return selectList;
        }

        var grouped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in groupBy.Replace("GROUP BY", "", StringComparison.OrdinalIgnoreCase)
                     .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            grouped.Add(token);
        }

        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in selectList.Split(',', StringSplitOptions.TrimEntries))
        {
            var asIdx = part.LastIndexOf(" AS ", StringComparison.OrdinalIgnoreCase);
            selected.Add(asIdx >= 0 ? part[(asIdx + 4)..].Trim() : part.Trim());
        }

        var extras = new List<string>();
        foreach (var segment in orderBy.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var column = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
            if (string.IsNullOrWhiteSpace(column) ||
                grouped.Contains(column) ||
                selected.Contains(column) ||
                !IsSimpleSqlIdent(column))
            {
                continue;
            }

            extras.Add($"MAX({column}) AS {column}");
            selected.Add(column);
        }

        return extras.Count == 0 ? selectList : selectList + ", " + string.Join(", ", extras);
    }

    private static bool IsSimpleSqlIdent(string value)
    {
        if (value.Length == 0 || (!char.IsLetter(value[0]) && value[0] != '_'))
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!char.IsLetterOrDigit(ch) && ch is not '_' and not '.')
            {
                return false;
            }
        }

        return true;
    }

    private static bool ShouldAggregateCategoryTotals(DiagramDefinition diagram)
    {
        if (!DiagramBindings.IsCategoryChart(diagram.Kind))
        {
            return false;
        }

        if (diagram.Properties.ContainsKey("x_step"))
        {
            return false;
        }

        if (diagram.Properties.TryGetValue("series", out var series) &&
            !string.IsNullOrWhiteSpace(series))
        {
            return false;
        }

        return true;
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
        foreach (var column in DiagramBindings.SelectedSqlColumns(card.Diagram, card.Tooltip))
        {
            names.Add(column);
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

        if (DiagramBindings.TryGetColumn(card.Diagram, "x", out var category))
        {
            return $"ORDER BY {category}";
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
            FilterKind.Date => BuildDateClause(filters.GetDate(definition.Name), definition, filters, parameters, sqlDialect),
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
        FilterState filters,
        List<QueryParameter> parameters,
        SqlDialect sqlDialect)
    {
        if (range is null)
        {
            return null;
        }

        var variable = definition.Name;
        var column = ResolveColumnName(definition.ColumnReference, variable);

        if (!string.IsNullOrWhiteSpace(definition.GrainFilterName))
        {
            var grain = PeriodAnchorResolver.TryReadGrain(filters, definition.GrainFilterName);
            var anchor = PeriodAnchorResolver.ResolveAnchor(range.Value.From, grain);
            var param = $"@{variable}_anchor";
            parameters.Add(new QueryParameter(param, anchor));
            return $"{column} = {param}";
        }

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
