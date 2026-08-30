#nullable enable

namespace DashSpec.Host.Commands;

internal static class FilterCommandPaths
{
    public const string FilterBranch = "filter";
    public const string ReportBranch = "report";
    public const string PageBranch = "page";

    public static string FilterPath(string filterName) => $"select filter {filterName}";

    public static string? ReadBranchArg(string canonicalPath, string branch)
    {
        var prefix = $"select {branch} ";
        if (!canonicalPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return canonicalPath[prefix.Length..].Trim();
    }

    public static string? ReadFilterName(string canonicalPath)
    {
        const string prefix = "select filter ";
        if (!canonicalPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var tail = canonicalPath[prefix.Length..].Trim();
        if (tail.Length == 0)
        {
            return null;
        }

        var spaceIndex = tail.IndexOf(' ');
        return spaceIndex < 0 ? tail : tail[..spaceIndex];
    }
}
