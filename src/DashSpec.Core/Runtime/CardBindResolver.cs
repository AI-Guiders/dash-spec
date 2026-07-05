namespace DashSpec.Core.Runtime;

public static class CardBindResolver
{
    public const string DashboardToken = "dashboard";

    public static IReadOnlyList<string> Expand(
        IReadOnlyList<string> explicitBind,
        IReadOnlyList<string> localFilters,
        IReadOnlyList<string> dashboardFilters)
    {
        var result = new List<string>();

        foreach (var name in explicitBind)
        {
            if (string.Equals(name, DashboardToken, StringComparison.OrdinalIgnoreCase))
            {
                result.AddRange(dashboardFilters);
                continue;
            }

            result.Add(name);
        }

        foreach (var local in localFilters)
        {
            if (!result.Contains(local, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(local);
            }
        }

        return result;
    }
}
