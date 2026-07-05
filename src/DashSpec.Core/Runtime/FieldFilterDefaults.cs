namespace DashSpec.Core.Runtime;

public static class FieldFilterDefaults
{
    public static IReadOnlyList<string> ResolveValues(string? defaultExpression)
    {
        if (string.IsNullOrWhiteSpace(defaultExpression))
        {
            return [];
        }

        return defaultExpression
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }
}
