using DashSpec.Abstractions.Data;

namespace DashSpec.Core.Tests;

/// <summary>Test-only row fixtures (DSPEC030 does not apply to test projects).</summary>
internal static class RowBatchTestKit
{
    public static RowBatch FromDictionaries(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        if (rows.Count == 0)
        {
            return RowBatch.Empty;
        }

        var columnNames = rows[0].Keys.ToList();
        var values = rows.Select(r =>
        {
            var cells = new object?[columnNames.Count];
            for (var i = 0; i < columnNames.Count; i++)
            {
                cells[i] = r.TryGetValue(columnNames[i], out var v) ? v : null;
            }

            return cells;
        }).ToList();

        return RowBatch.Create(columnNames, values);
    }
}
