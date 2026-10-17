using System.Text;
using DashSpec.Abstractions.Data;
using DashSpec.Abstractions.Query;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Compilation.Dialects;
using DashSpec.Execution.Runtime;

namespace DashSpec.Execution.Compilation;

public static class QueryCompiler
{
    public static CompiledQuery Compile(
        CardDefinition card,
        FilterState filters,
        IReadOnlyDictionary<string, FilterDefinition> filterDefinitions,
        RowTypeCatalog rowTypes,
        SqlDialect? sqlDialect = null,
        string? specDirectory = null,
        CardCellDrillOverlay? cellDrillOverlay = null,
        ReportTimePolicy? reportTimePolicy = null) =>
        Compile(card, filters, filterDefinitions, rowTypes, SqlDialectResolver.Resolve(sqlDialect ?? SqlDialect.TSql), specDirectory, cellDrillOverlay, reportTimePolicy);

    public static CompiledQuery Compile(
        CardDefinition card,
        FilterState filters,
        IReadOnlyDictionary<string, FilterDefinition> filterDefinitions,
        DashboardDocument document,
        SqlDialect? sqlDialect = null,
        string? specDirectory = null,
        CardCellDrillOverlay? cellDrillOverlay = null,
        ReportTimePolicy? reportTimePolicy = null) =>
        Compile(
            card,
            filters,
            filterDefinitions,
            RowTypeCatalog.FromDocument(document),
            SqlDialectResolver.Resolve(sqlDialect ?? SqlDialect.TSql),
            specDirectory,
            cellDrillOverlay,
            reportTimePolicy,
            ResolveFilterDerives(document));

    public static CompiledQuery Compile(
        CardDefinition card,
        FilterState filters,
        IReadOnlyDictionary<string, FilterDefinition> filterDefinitions,
        RowTypeCatalog rowTypes,
        ISqlDialectBackend dialect,
        string? specDirectory = null,
        CardCellDrillOverlay? cellDrillOverlay = null,
        ReportTimePolicy? reportTimePolicy = null,
        IReadOnlyDictionary<string, FilterDeriveDefinition>? filterDerives = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(filterDefinitions);
        ArgumentNullException.ThrowIfNull(rowTypes);
        ArgumentNullException.ThrowIfNull(dialect);

        var rowSchema = rowTypes.GetRequired(card.DataSource.RowsType);

        var fromClause = card.DataSource.Kind switch
        {
            DataSourceKind.View => card.DataSource.Value,
            DataSourceKind.Sql => WrapSqlDataSource(
                SqlDataSourceResolver.ResolveSqlBody(card.DataSource, specDirectory)),
            DataSourceKind.Xlsx => BuildXlsxOpenRowSet(card.DataSource, specDirectory, dialect),
            _ => throw new ArgumentOutOfRangeException(nameof(card)),
        };

        var parameters = new List<QueryParameter>();
        var whereBuilder = new StringBuilder("WHERE 1=1");
        AppendBoundFilters(whereBuilder, card, filters, filterDefinitions, parameters, dialect, cellDrillOverlay, rowSchema, filterDerives);
        AppendWorkWindowClip(whereBuilder, reportTimePolicy, dialect);

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
                    dialect),
                parameters,
                rowSchema);
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
                    dialect),
                parameters,
                rowSchema);
        }

        return new CompiledQuery(
            BuildSelectSql(
                ResolveSelectColumns(card),
                fromClause,
                whereBuilder.ToString(),
                groupBy: null,
                ResolveOrderBy(card),
                tableLimit,
                dialect),
            parameters,
            rowSchema);
    }

    private static string BuildSelectSql(
        string selectList,
        string fromClause,
        string whereClause,
        string? groupBy,
        string orderBy,
        int tableLimit,
        ISqlDialectBackend dialect)
    {
        var sql = new StringBuilder();
        dialect.AppendSelect(
            sql,
            new SqlSelectParts(selectList, fromClause, whereClause, groupBy, orderBy, tableLimit));
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

        if (DiagramBindings.TryGetColumn(diagram, "color", out var color))
        {
            parts.Add($"MAX({color}) AS {color}");
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

    private static string BuildXlsxOpenRowSet(
        DataSourceDefinition source,
        string? specDirectory,
        ISqlDialectBackend dialect)
    {
        if (!string.Equals(dialect.Id, "tsql", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "datasource xlsx is SQL Server OPENROWSET and requires sqldialect = tsql.");
        }

        var path = TSqlXlsxOpenRowSet.ResolvePath(source.Value, specDirectory);
        return TSqlXlsxOpenRowSet.Format(path, source.Sheet?.Value);
    }

    private static string WrapSqlDataSource(string rawSql)
    {
        SqlReadOnlyValidator.ValidateSqlBody(rawSql);
        return $"({rawSql.Trim().TrimEnd(';')}) AS _dashspec_q";
    }

    /// <summary>Field filters need <c>table.column</c> to query DISTINCT options (wire-only / click-set filters may omit the table).</summary>
    public static bool CanLoadDistinctFieldOptions(FilterDefinition filter)
    {
        var columnReference = filter.ColumnReference?.Value;
        if (filter.Kind != FilterKind.Field || string.IsNullOrWhiteSpace(columnReference))
        {
            return false;
        }

        var lastDot = columnReference.LastIndexOf('.');
        return lastDot > 0 && lastDot < columnReference.Length - 1;
    }

    public static string BuildDistinctFieldSql(FilterDefinition filter)
    {
        if (!CanLoadDistinctFieldOptions(filter))
        {
            var columnReference = filter.ColumnReference?.Value;
            throw new ArgumentException(
                filter.Kind != FilterKind.Field || string.IsNullOrWhiteSpace(columnReference)
                    ? "Field filter requires column reference."
                    : $"Invalid column reference '{columnReference}'.",
                nameof(filter));
        }

        var columnRef = filter.ColumnReference!.Value;
        var lastDot = columnRef.LastIndexOf('.');

        var table = columnRef[..lastDot];
        var column = columnRef[(lastDot + 1)..];
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
        foreach (var column in DiagramBindings.SelectedSqlColumns(card.Diagram, card.Tooltip?.Value))
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

        if (string.Equals(card.Diagram.Kind, "gantt", StringComparison.OrdinalIgnoreCase) &&
            DiagramBindings.TryGetColumn(card.Diagram, "from", out var ganttStart))
        {
            return $"ORDER BY {ganttStart}";
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
        ISqlDialectBackend dialect,
        CardCellDrillOverlay? cellDrillOverlay,
        RowTypeSchema rowSchema,
        IReadOnlyDictionary<string, FilterDeriveDefinition>? filterDerives)
    {
        foreach (var filterName in card.BoundFilters)
        {
            if (!filterDefinitions.TryGetValue(filterName, out var definition) ||
                definition.Kind is not FilterKind.Date and not FilterKind.Field)
            {
                continue;
            }

            // ADR-0036 derive (usage_date from period_start): derived target filter is a
            // UI-level alias — when the source row schema lacks the target column, push the
            // clause on the derive SOURCE column with the target's values.
            var sqlDefinition = definition;
            var derived = false;
            if (!SchemaContainsColumn(rowSchema, ResolveColumnName(definition.ColumnReference?.Value, definition.Name)) &&
                filterDerives is not null &&
                filterDerives.TryGetValue(filterName, out var derive) &&
                !string.IsNullOrWhiteSpace(derive.SourceFilter))
            {
                FilterDefinition? sourceDefinition = null;
                var sourceColumnKnown = filterDefinitions.TryGetValue(derive.SourceFilter, out sourceDefinition)
                    ? SchemaContainsColumn(rowSchema, ResolveColumnName(sourceDefinition.ColumnReference?.Value, sourceDefinition.Name))
                    : SchemaContainsColumn(rowSchema, derive.SourceFilter);
                if (sourceColumnKnown)
                {
                    sqlDefinition = sourceDefinition ?? definition.WithColumnReference(
                        Microsoft.FSharp.Core.FSharpOption<string>.Some(derive.SourceFilter));
                    derived = true;
                }
            }

            var clause = derived
                ? BuildDerivedClause(definition, sqlDefinition, filters, parameters, dialect, cellDrillOverlay)
                : BuildClause(definition, filters, parameters, dialect, cellDrillOverlay);
            if (clause is null)
            {
                continue;
            }

            whereBuilder.Append(" AND ").Append(clause);
        }
    }

    private static string? BuildDerivedClause(
        FilterDefinition target,
        FilterDefinition source,
        FilterState filters,
        List<QueryParameter> parameters,
        ISqlDialectBackend dialect,
        CardCellDrillOverlay? cellDrillOverlay)
    {
        return target.Kind switch
        {
            FilterKind.Date when cellDrillOverlay?.TryGetDate(target.Name, out var drillDate) == true =>
                BuildDateClause(drillDate, source, filters, parameters, dialect),
            FilterKind.Field when cellDrillOverlay?.TryGetField(target.Name, out var drillField) == true =>
                BuildFieldClause(new FieldFilterValue(drillField.ToList()), source, parameters),
            FilterKind.Date => BuildDateClause(filters.GetDate(target.Name), source, filters, parameters, dialect),
            FilterKind.Field => BuildFieldClause(filters.GetField(target.Name), source, parameters),
            _ => null,
        };
    }

    private static bool SchemaContainsColumn(RowTypeSchema schema, string column)
    {
        foreach (var field in schema.Fields)
        {
            if (string.Equals(field.Name, column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var dot = column.LastIndexOf('.');
            if (dot >= 0 && string.Equals(field.Name, column[(dot + 1)..], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyDictionary<string, FilterDeriveDefinition> ResolveFilterDerives(DashboardDocument document)
    {
        var map = new Dictionary<string, FilterDeriveDefinition>(StringComparer.OrdinalIgnoreCase);
        if (document.Pages?.Value is not { } pages)
        {
            return map;
        }

        foreach (var page in pages)
        {
            var derive = page.UsageDateDerive?.Value;
            if (derive is not null && !map.ContainsKey(derive.TargetFilter))
            {
                map[derive.TargetFilter] = derive;
            }
        }

        return map;
    }

    private static string? BuildClause(
        FilterDefinition definition,
        FilterState filters,
        List<QueryParameter> parameters,
        ISqlDialectBackend dialect,
        CardCellDrillOverlay? cellDrillOverlay)
    {
        return definition.Kind switch
        {
            FilterKind.Date when cellDrillOverlay?.TryGetDate(definition.Name, out var drillDate) == true =>
                BuildDateClause(drillDate, definition, filters, parameters, dialect),
            FilterKind.Field when cellDrillOverlay?.TryGetField(definition.Name, out var drillField) == true =>
                BuildFieldClause(new FieldFilterValue(drillField.ToList()), definition, parameters),
            FilterKind.Date => BuildDateClause(filters.GetDate(definition.Name), definition, filters, parameters, dialect),
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
        ISqlDialectBackend dialect)
    {
        if (range is null)
        {
            return null;
        }

        var variable = definition.Name;
        var column = ResolveColumnName(definition.ColumnReference?.Value, variable);

        if (!string.IsNullOrWhiteSpace(definition.GrainFilterName?.Value))
        {
            var grain = PeriodAnchorResolver.TryReadGrain(filters, definition.GrainFilterName?.Value);
            var anchor = PeriodAnchorResolver.ResolveAnchor(range.Value.From, grain);
            var param = $"@{variable}_anchor";
            parameters.Add(new QueryParameter(param, anchor));
            return $"{column} = {param}";
        }

        var fromParam = $"@{variable}_from";
        var toParam = $"@{variable}_to";
        parameters.Add(new QueryParameter(fromParam, range.Value.From));
        parameters.Add(new QueryParameter(toParam, range.Value.To));

        var upperExclusive = dialect.FormatDateRangeUpperExclusive(toParam);

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
        var column = ResolveColumnName(definition.ColumnReference?.Value, variable);

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

    private static void AppendWorkWindowClip(
        StringBuilder whereBuilder,
        ReportTimePolicy? reportTimePolicy,
        ISqlDialectBackend dialect)
    {
        if (reportTimePolicy is null)
        {
            return;
        }

        var predicate = WorkWindowSql.TryBuildWorkClipPredicate(reportTimePolicy, dialect.Id);
        if (string.IsNullOrWhiteSpace(predicate))
        {
            return;
        }

        whereBuilder.Append(" AND ").Append(predicate);
    }
}
