using DashSpec.Abstractions.Data;

namespace DashSpec.Execution.Runtime;

/// <summary>
/// ADR-0120 tail reduce — the runtime face of the <c>series max = N</c> sugar.
/// Groups rows by the <c>by</c> column, ranks groups by their aggregate weight
/// (sum of the <c>value</c> column when the batch has one, else row count), keeps
/// the top <c>top</c> groups intact, and folds every remaining group into one
/// tail group labelled <c>tail</c> (default "Другие"); without a tail label the
/// tail groups are dropped. No special node kind — this is just the <c>reduce</c>
/// transform plugin executing on a typed batch.
/// </summary>
public static class ReduceBatchTransform
{
    public const string DefaultTailLabel = "Другие";

    public static TypedRowBatch Apply(
        TypedRowBatch batch,
        IReadOnlyList<DashflowTransformParameterDefinition> parameters)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(parameters);

        var by = Parameter(parameters, "by") ?? throw new InvalidOperationException(
            "reduce step is missing 'by' (re-parse the spec: series max synthesizes by=series).");

        var top = int.TryParse(Parameter(parameters, "top"), out var parsed) && parsed > 0
            ? parsed
            : throw new InvalidOperationException("reduce step requires a positive 'top'.");

        var tailLabel = Parameter(parameters, "tail") ?? DefaultTailLabel;

        if (batch.IsEmpty || !batch.Schema.FieldOrdinals.ContainsKey(by))
        {
            return batch;
        }

        var hasValue = batch.Schema.FieldOrdinals.ContainsKey("value");

        // group weight: sum of `value` when present, otherwise the row count
        var weights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in batch.Rows)
        {
            var key = row.Get(by).ToClr()?.ToString() ?? string.Empty;
            var weight = hasValue ? row.GetDoubleOrNull("value") ?? 0d : 1d;
            weights.TryGetValue(key, out var current);
            weights[key] = current + weight;
        }

        var keep = weights
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(top)
            .Select(kv => kv.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var folded = weights.Keys.Where(k => !keep.Contains(k)).ToList();

        var rowValues = new List<DashValue[]>(batch.Count);
        foreach (var row in batch.Rows)
        {
            var cells = new DashValue[batch.Schema.FieldCount];
            for (var i = 0; i < batch.Schema.FieldCount; i++)
            {
                cells[i] = row.Get(batch.Schema.Fields[i].Name);
            }

            var key = row.Get(by).ToClr()?.ToString() ?? string.Empty;
            if (!keep.Contains(key))
            {
                if (tailLabel.Length == 0)
                {
                    continue; // no tail label: tail groups are dropped
                }

                cells[batch.Schema.FieldOrdinals[by]] = DashValue.FromString(tailLabel);
            }

            rowValues.Add(cells);
        }

        return TypedRowBatch.Create(batch.Schema, rowValues);
    }

    /// <summary>Folded group names — "Другие (M)" widget entries and the honest
    /// count ride the same data the matrix sees.</summary>
    public static IReadOnlyList<string> FoldedGroups(
        TypedRowBatch batch,
        IReadOnlyList<DashflowTransformParameterDefinition> parameters)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var by = Parameter(parameters, "by");
        if (batch.IsEmpty || by is null || !batch.Schema.FieldOrdinals.ContainsKey(by))
        {
            return [];
        }

        var keep = Apply(batch, parameters); // same ranking; read the tail off the result
        var tail = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in keep.Rows)
        {
            var key = row.Get(by).ToClr()?.ToString();
            if (key is not null)
            {
                tail.Add(key);
            }
        }

        var original = batch.Rows
            .Select(r => r.Get(by).ToClr()?.ToString() ?? string.Empty)
            .Where(k => !tail.Contains(k) && k.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return original;
    }

    private static string? Parameter(IReadOnlyList<DashflowTransformParameterDefinition> parameters, string key) =>
        parameters.FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;
}
