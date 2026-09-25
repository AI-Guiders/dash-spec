namespace DashSpec.Core.Parsing;

/// <summary>Include / glob merge order (ADR-0024, ADR-0057 §include).</summary>
public static class IncludeMergePolicy
{
    /// <summary>Deterministic glob expansion order — ordinal ignore-case full path.</summary>
    public static IReadOnlyList<string> OrderGlobPaths(IEnumerable<string> paths) =>
        paths
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Later paths in ordered glob expansion win on duplicate module ids.</summary>
    public static void EnsureUniqueModuleId(string moduleId, string path, ISet<string> seenIds)
    {
        if (!seenIds.Add(moduleId))
        {
            throw new DashSpecParseException(
                $"Duplicate module id '{moduleId}' after include merge (last at '{path}').");
        }
    }
}
