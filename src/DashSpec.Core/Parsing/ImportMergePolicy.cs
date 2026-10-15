namespace DashSpec.Core.Parsing;

/// <summary>Import / glob merge order (ADR-0024, ADR-0057).</summary>
public static class ImportMergePolicy
{
    /// <summary>Deterministic glob expansion order — ordinal ignore-case full path.</summary>
    public static IReadOnlyList<string> OrderGlobPaths(IEnumerable<string> paths) =>
        paths
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
