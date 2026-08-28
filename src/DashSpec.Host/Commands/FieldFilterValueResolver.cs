#nullable enable
using DashSpec.Core.Model;

namespace DashSpec.Host.Commands;

internal static class FieldFilterValueResolver
{
    public static bool TryResolveValues(
        string argTail,
        FilterDefinition filter,
        IReadOnlyList<string> options,
        out IReadOnlyList<string> values,
        out string? error)
    {
        values = [];
        error = null;
        var tail = argTail.Trim();
        if (tail.Length == 0)
        {
            error = "Field value is required.";
            return false;
        }

        if (tail.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            values = [];
            return true;
        }

        if (tail.StartsWith('[') && tail.EndsWith(']'))
        {
            var tokens = ParseList(tail);
            if (tokens.Count == 0)
            {
                error = "Field list cannot be empty.";
                return false;
            }

            return TryResolveTokens(tokens, options, out values, out error);
        }

        if (!TryResolveToken(tail, options, out var single, out error))
        {
            return false;
        }

        values = [single];
        return true;
    }

    static bool TryResolveTokens(
        IReadOnlyList<string> tokens,
        IReadOnlyList<string> options,
        out IReadOnlyList<string> values,
        out string? error)
    {
        var resolved = new List<string>();
        foreach (var token in tokens)
        {
            if (!TryResolveToken(token, options, out var match, out error))
            {
                values = [];
                return false;
            }

            resolved.Add(match);
        }

        values = resolved
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        error = null;
        return true;
    }

    static bool TryResolveToken(
        string token,
        IReadOnlyList<string> options,
        out string match,
        out string? error)
    {
        match = "";
        error = null;
        if (token.Length == 0)
        {
            error = "Empty field value.";
            return false;
        }

        var exact = options
            .Where(option => option.Equals(token, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (exact.Count == 1)
        {
            match = exact[0];
            return true;
        }

        if (exact.Count > 1)
        {
            error = $"Ambiguous value '{token}': {string.Join(", ", exact)}";
            return false;
        }

        var contains = options
            .Where(option => option.Contains(token, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (contains.Count == 1)
        {
            match = contains[0];
            return true;
        }

        if (contains.Count > 1)
        {
            error = $"Ambiguous value '{token}': {string.Join(", ", contains)}";
            return false;
        }

        if (options.Count == 0)
        {
            match = token;
            return true;
        }

        error = $"No match for '{token}'.";
        return false;
    }

    static List<string> ParseList(string bracketed)
    {
        var inner = bracketed[1..^1];
        return inner
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }
}
