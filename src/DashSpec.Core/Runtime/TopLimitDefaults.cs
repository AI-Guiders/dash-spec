using DashSpec.Core.Model;

namespace DashSpec.Core.Runtime;

public static class TopLimitDefaults
{
    public const int DefaultMax = 10_000;

    public static int Resolve(FilterDefinition definition, int? current)
    {
        var min = definition.MinValue ?? 1;
        var max = definition.MaxValue ?? DefaultMax;
        var value = current ?? ParseDefault(definition);
        return Math.Clamp(value, min, max);
    }

    public static int ParseDefault(FilterDefinition definition)
    {
        if (definition.Kind is not FilterKind.Top)
        {
            throw new ArgumentException($"Filter '{definition.Name}' is not a top filter.", nameof(definition));
        }

        if (!int.TryParse(definition.DefaultExpression, out var parsed) || parsed <= 0)
        {
            throw new InvalidOperationException(
                $"Top filter '{definition.Name}' requires numeric default, e.g. default = 200.");
        }

        return parsed;
    }
}
