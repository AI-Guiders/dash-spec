using System;

namespace DashSpec.Analyzers;

/// <summary>
/// Logical path normalization aligned with <c>AIGuiders.Platform.Modeling.Paths.LogicalPath</c> (GUIDERS-ADR-0050).
/// Analyzer projects must stay netstandard2.0 (RS1041) and avoid file I/O (RS1035), so we do not reference the Paths package here.
/// </summary>
internal readonly struct LogicalPathCompat
{
    public string Value { get; }

    private LogicalPathCompat(string value)
    {
        Value = value;
    }

    public static LogicalPathCompat Create(string raw) => new LogicalPathCompat(Normalize(raw));

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public bool StartsWith(LogicalPathCompat prefix) =>
        !prefix.IsEmpty
        && Value.StartsWith(prefix.Value, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var normalized = raw.Trim().Replace('\\', '/');
        while (normalized.Contains("//"))
        {
            normalized = normalized.Replace("//", "/");
        }

        return normalized.TrimStart('.').TrimStart('/').TrimEnd('/');
    }

    /// <summary>Match repo-relative layer prefix inside a normalized physical or logical path.</summary>
    public static bool ContainsLayerSegment(string? physicalOrLogicalPath, string repoRelativePrefix)
    {
        if (string.IsNullOrWhiteSpace(physicalOrLogicalPath))
        {
            return false;
        }

        var normalized = Normalize(physicalOrLogicalPath!);
        var prefix = Normalize(repoRelativePrefix);
        if (string.IsNullOrEmpty(prefix))
        {
            return false;
        }

        if (normalized.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, prefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return normalized.Contains("/" + prefix + "/", StringComparison.OrdinalIgnoreCase)
               || normalized.EndsWith("/" + prefix, StringComparison.OrdinalIgnoreCase);
    }
}
