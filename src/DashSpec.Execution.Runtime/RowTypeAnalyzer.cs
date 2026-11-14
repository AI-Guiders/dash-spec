using DashSpec.Abstractions.Data;

namespace DashSpec.Execution.Runtime;

internal static class RowTypeAnalyzer
{
    public static void Validate(DashboardDocument document)
    {
        var catalog = document.ResolvedRowTypeSchemas;

        foreach (var card in document.Cards)
        {
            ValidateDataSource(card.Id, card.DataSource, catalog);
            ValidateDiagram(card.Id, card.Diagram, card.DataSource, catalog, card.Tooltip?.Value);

            if (card.DiagramSlots is null)
            {
                continue;
            }

            foreach (var slot in card.DiagramSlots.Values)
            {
                ValidateDataSource($"{card.Id}/{slot.SlotRef}", slot.DataSource, catalog);
                ValidateDiagram($"{card.Id}/{slot.SlotRef}", slot.Diagram, slot.DataSource, catalog);
            }
        }
    }

    private static void ValidateDiagram(
        string context,
        DiagramDefinition diagram,
        DataSourceDefinition source,
        IReadOnlyDictionary<string, RowTypeSchema> catalog,
        TooltipDefinition? cardTooltip = null)
    {
        DiagramBindings.ValidateRequiredBindings(context, diagram);

        if (string.IsNullOrWhiteSpace(source.RowsType)
            || !catalog.TryGetValue(source.RowsType, out var schema))
        {
            return;
        }

        var known = new HashSet<string>(schema.Fields.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);

        // Every bound column — required or optional — is a contract against the row
        // type. A typo today dies as a silently empty chart (row lookups miss) or an
        // SQL error at first render; both are spec defects visible at compile.
        foreach (var role in DiagramBindings.SelectColumnRoles(diagram.Kind))
        {
            if (!DiagramBindings.TryGetColumn(diagram, role, out var column))
            {
                continue;
            }

            if (!schema.FieldOrdinals.TryGetValue(column, out var ordinal))
            {
                throw new DashSpec.Modeling.Core.DashSpecFlowException(
                    DashSpec.Modeling.Core.DashSpecRules.formatMessage(
                        "rule.TypeMismatch.column_not_in_type",
                        context,
                        diagram.Kind,
                        role,
                        column,
                        source.RowsType,
                        string.Join(", ", schema.Fields.Select(f => f.Name))));
            }

            ValidateRoleTypeCompatibility(context, diagram, role, schema.Fields[ordinal], source.RowsType);
        }

        // Tooltip variable columns ride the same SELECT — same contract.
        if (cardTooltip is { } cardTooltipValue)
        {
            foreach (var column in TooltipTemplate.SelectColumns(cardTooltipValue))
            {
                if (!known.Contains(column))
                {
                    throw new DashSpec.Modeling.Core.DashSpecFlowException(
                        DashSpec.Modeling.Core.DashSpecRules.formatMessage(
                            "rule.TypeMismatch.tooltip_column_missing",
                            context,
                            column,
                            source.RowsType,
                            string.Join(", ", schema.Fields.Select(f => f.Name))));
                }
            }
        }

        // A table's column list is the same contract, in its own surface grammar.
        if (diagram.Kind.Equals("table", StringComparison.OrdinalIgnoreCase)
            && diagram.Properties.TryGetValue("columns", out var columns)
            && !string.IsNullOrWhiteSpace(columns))
        {
            foreach (var column in columns.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (!known.Contains(column))
                {
                    throw new DashSpec.Modeling.Core.DashSpecFlowException(
                        DashSpec.Modeling.Core.DashSpecRules.formatMessage(
                            "rule.TypeMismatch.table_column_missing",
                            context,
                            column,
                            source.RowsType,
                            string.Join(", ", schema.Fields.Select(f => f.Name))));
                }
            }
        }

        // `sort = value DESC, country` rides the compiled ORDER BY — a typo reaches
        // SQL. Each comma segment is a column with an optional ASC/DESC direction.
        if (diagram.Properties.TryGetValue("sort", out var sort)
            && !string.IsNullOrWhiteSpace(sort))
        {
            foreach (var segment in sort.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var words = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var column = words[0];
                if (!known.Contains(column))
                {
                    throw new DashSpec.Modeling.Core.DashSpecFlowException(
                        DashSpec.Modeling.Core.DashSpecRules.formatMessage(
                            "rule.TypeMismatch.sort_column_missing",
                            context,
                            column,
                            source.RowsType,
                            string.Join(", ", schema.Fields.Select(f => f.Name))));
                }

                if (words.Length > 2
                    || (words.Length == 2
                        && !words[1].Equals("ASC", StringComparison.OrdinalIgnoreCase)
                        && !words[1].Equals("DESC", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new DashSpec.Modeling.Core.DashSpecFlowException(
                        DashSpec.Modeling.Core.DashSpecRules.formatMessage(
                            "rule.TypeMismatch.sort_segment_invalid",
                            context,
                            segment));
                }
            }
        }
    }

    private static void ValidateRoleTypeCompatibility(
        string context,
        DiagramDefinition diagram,
        string role,
        RowFieldSchema field,
        string rowTypeName)
    {
        var kind = diagram.Kind.ToLowerInvariant();
        var r = role.ToLowerInvariant();

        var isCountAggregate =
            diagram.Properties.TryGetValue("aggregate", out var agg) &&
            (agg.Equals("count", StringComparison.OrdinalIgnoreCase) ||
             agg.Equals("distinct_count", StringComparison.OrdinalIgnoreCase));

        var requiresNumeric = (kind, r) switch
        {
            ("heatmap" or "gauge" or "histogram" or "box" or "boxplot", "value") => !isCountAggregate,
            ("bar" or "pie" or "donut" or "doughnut" or "treemap" or "windrose" or "wind_rose" or "line" or "area" or "sparkline", "y" or "reference") => !isCountAggregate,
            ("scatter", "x" or "y" or "size") => !isCountAggregate,
            _ => false,
        };

        if (requiresNumeric &&
            field.Kind is not (DashPrimitiveKind.Int or DashPrimitiveKind.Decimal or DashPrimitiveKind.Duration))
        {
            throw new DashSpec.Modeling.Core.DashSpecFlowException(
                DashSpec.Modeling.Core.DashSpecRules.formatMessage(
                    "rule.TypeMismatch.role_requires_numeric",
                    context,
                    diagram.Kind,
                    role,
                    field.Name,
                    rowTypeName,
                    field.Kind.ToString().ToLowerInvariant()));
        }

        if (kind is "gantt" or "gantt_timeline" &&
            r is "from" or "to" &&
            field.Kind is not (DashPrimitiveKind.Date or DashPrimitiveKind.DateTime or DashPrimitiveKind.Time or DashPrimitiveKind.String))
        {
            throw new DashSpec.Modeling.Core.DashSpecFlowException(
                DashSpec.Modeling.Core.DashSpecRules.formatMessage(
                    "rule.TypeMismatch.role_requires_temporal",
                    context,
                    diagram.Kind,
                    role,
                    field.Name,
                    rowTypeName,
                    field.Kind.ToString().ToLowerInvariant()));
        }
    }

    private static void ValidateDataSource(
        string context,
        DataSourceDefinition source,
        IReadOnlyDictionary<string, RowTypeSchema> catalog)
    {
        if (string.IsNullOrWhiteSpace(source.RowsType))
        {
            return;
        }

        if (!catalog.ContainsKey(source.RowsType))
        {
            throw new DashSpec.Modeling.Core.DashSpecFlowException(
                DashSpec.Modeling.Core.DashSpecRules.formatMessage(
                    "rule.UnresolvedImportOrReference.row_type",
                    context,
                    source.RowsType));
        }
    }
}
