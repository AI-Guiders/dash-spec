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
            ValidateDiagram(card.Id, card.Diagram, card.DataSource, catalog);

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
        IReadOnlyDictionary<string, RowTypeSchema> catalog)
    {
        DiagramBindings.ValidateRequiredBindings(context, diagram);

        // A table's column list is a contract against the row type — a typo there
        // is a spec defect visible at compile, not an SQL error at first render.
        if (diagram.Kind.Equals("table", StringComparison.OrdinalIgnoreCase)
            && diagram.Properties.TryGetValue("columns", out var columns)
            && !string.IsNullOrWhiteSpace(columns)
            && !string.IsNullOrWhiteSpace(source.RowsType)
            && catalog.TryGetValue(source.RowsType, out var schema))
        {
            var known = new HashSet<string>(schema.Fields.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
            foreach (var column in columns.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (!known.Contains(column))
                {
                    throw new DashSpec.Modeling.Core.DashSpecFlowException(
                        $"Card '{context}': table column '{column}' is not a field of row type " +
                        $"'{source.RowsType}' (fields: {string.Join(", ", schema.Fields.Select(f => f.Name))}).");
                }
            }
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
                $"Card '{context}': row type '{source.RowsType}' is not defined in the document catalog " +
                "— declare it among the document types or check the spelling.");
        }
    }
}
