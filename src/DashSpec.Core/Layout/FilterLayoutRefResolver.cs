using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Layout;

internal static class FilterLayoutRefResolver
{
    public static string Resolve(string token, IReadOnlyList<FilterDefinition> filters, string context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        string? byRef = null;
        string? byName = null;

        foreach (var filter in filters)
        {
            if (!string.IsNullOrWhiteSpace(filter.LayoutRef) &&
                string.Equals(filter.LayoutRef, token, StringComparison.OrdinalIgnoreCase))
            {
                if (byRef is not null)
                {
                    throw new DashSpecParseException(
                        $"{context}: layout token '{token}' matches more than one filter ref.");
                }

                byRef = filter.Name;
            }

            if (string.Equals(filter.Name, token, StringComparison.OrdinalIgnoreCase))
            {
                if (byName is not null)
                {
                    throw new DashSpecParseException(
                        $"{context}: layout token '{token}' matches more than one filter name.");
                }

                byName = filter.Name;
            }
        }

        if (byRef is not null && byName is not null && !string.Equals(byRef, byName, StringComparison.OrdinalIgnoreCase))
        {
            throw new DashSpecParseException(
                $"{context}: layout token '{token}' is ambiguous (matches both ref and name).");
        }

        var resolved = byRef ?? byName;
        if (resolved is null)
        {
            throw new DashSpecParseException(
                $"{context}: layout token '{token}' does not match any filter ref or name.");
        }

        return resolved;
    }
}
