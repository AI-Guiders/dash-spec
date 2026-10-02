using DashSpec.Abstractions.Data;
using DashSpec.Core.Model;

namespace DashSpec.Core.Analysis;

internal static class RowTypeAnalyzer
{
    public static void Validate(DashboardDocument document)
    {
        var catalog = document.ResolvedRowTypeSchemas;

        foreach (var card in document.Cards)
        {
            ValidateDataSource(card.Id, card.DataSource, catalog);

            if (card.DiagramSlots is null)
            {
                continue;
            }

            foreach (var slot in card.DiagramSlots.Values)
            {
                ValidateDataSource($"{card.Id}/{slot.SlotRef}", slot.DataSource, catalog);
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
            throw new InvalidOperationException(
                $"Card '{context}': row type '{source.RowsType}' is not defined (Modeling SSOT).");
        }
    }
}
