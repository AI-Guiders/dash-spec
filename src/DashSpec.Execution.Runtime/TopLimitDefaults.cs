using DashSpec.Core.Model;

namespace DashSpec.Execution.Runtime;

public static class TopLimitDefaults
{
    public const int DefaultMax = 10_000;

    public static int Resolve(FilterDefinition definition, int? current)
    {
        var min = definition.MinValue ?? 1;
        var max = definition.MaxValue ?? DefaultMax;
        var value = current ?? ParseDefault(definition);
        if (min == 0 && value == 0)
        {
            return 0;
        }

        var floor = min == 0 ? 1 : min;
        return Math.Clamp(value, floor, max);
    }

    public static int ParseDefault(FilterDefinition definition)
    {
        if (definition.Kind is not FilterKind.Top)
        {
            throw new ArgumentException($"Filter '{definition.Name}' is not a top filter.", nameof(definition));
        }

        if (!int.TryParse(definition.DefaultExpression, out var parsed)
            || parsed < 0
            || (parsed == 0 && definition.MinValue != 0))
        {
            throw new InvalidOperationException(
                $"Top filter '{definition.Name}' requires numeric default, e.g. default = 200 (or 0 when min = 0).");
        }

        return parsed;
    }
}
