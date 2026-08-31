#nullable enable

namespace DashSpec.Host.Commands;

internal static class ShowCommandPaths
{
    public const string RootVerb = "show";

    public static string SurfacePath(string surfaceId) => $"show {surfaceId}";

    public static string? ReadSurfaceId(string canonicalPath)
    {
        const string prefix = "show ";
        if (!canonicalPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return canonicalPath[prefix.Length..].Trim();
    }
}
