#nullable enable

namespace DashSpec.Host.Commands;

internal static class ViewCommandPaths
{
    public const string RootVerb = "view";

    public static string CardPath(string cardId) => $"view {cardId}";

    public static string ViewPath(string cardId, string viewId) => $"view {cardId} {viewId}";

    public static string? ReadCardId(string canonicalPath)
    {
        const string prefix = "view ";
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

    public static string? ReadViewId(string canonicalPath)
    {
        const string prefix = "view ";
        if (!canonicalPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var tail = canonicalPath[prefix.Length..].Trim();
        var spaceIndex = tail.IndexOf(' ');
        if (spaceIndex < 0)
        {
            return null;
        }

        return tail[(spaceIndex + 1)..].Trim();
    }
}
