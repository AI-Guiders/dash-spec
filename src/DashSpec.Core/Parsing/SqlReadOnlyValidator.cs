using System.Text.RegularExpressions;

namespace DashSpec.Core.Parsing;

/// <summary>Проверка текста <c>datasource sql</c> и имён <c>datasource view</c> при разборе .dashspec.</summary>
public static partial class SqlReadOnlyValidator
{
    private static readonly string[] ForbiddenKeywords =
    [
        "INSERT", "UPDATE", "DELETE", "MERGE", "TRUNCATE",
        "DROP", "ALTER", "CREATE", "EXEC", "EXECUTE",
        "GRANT", "REVOKE", "DENY", "BACKUP", "RESTORE",
        "SHUTDOWN", "DBCC", "CALL",
    ];

    public static void ValidateViewReference(string qualifiedName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedName);

        if (!ViewReferencePattern().IsMatch(qualifiedName))
        {
            throw new DashSpecParseException(
                $"datasource view must be a qualified name (schema.object), got '{qualifiedName}'.");
        }
    }

    public static void ValidateSqlBody(string sql)
    {
        var body = sql.Trim();
        if (body.Length == 0)
        {
            throw new DashSpecParseException("datasource sql must not be empty.");
        }

        if (body.Contains(';', StringComparison.Ordinal))
        {
            throw new DashSpecParseException(
                "datasource sql must be a single read-only SELECT (semicolons are not allowed).");
        }

        if (BlockCommentPattern().IsMatch(body) || LineCommentPattern().IsMatch(body))
        {
            throw new DashSpecParseException(
                "datasource sql must not contain SQL comments (-- or /* */).");
        }

        var scanTarget = StripStringLiterals(body);
        if (!StartsWithReadQuery(scanTarget))
        {
            throw new DashSpecParseException(
                "datasource sql must start with SELECT or WITH (read-only query).");
        }

        if (SelectIntoPattern().IsMatch(scanTarget))
        {
            throw new DashSpecParseException(
                "datasource sql must not use SELECT INTO.");
        }

        foreach (var keyword in ForbiddenKeywords)
        {
            if (ContainsWholeWord(scanTarget, keyword))
            {
                throw new DashSpecParseException(
                    $"datasource sql must be read-only; forbidden keyword '{keyword}'.");
            }
        }

        if (DangerousTokenPattern().IsMatch(scanTarget))
        {
            throw new DashSpecParseException(
                "datasource sql contains a disallowed token (e.g. OPENROWSET, xp_, sp_executesql).");
        }
    }

    private static bool StartsWithReadQuery(string text)
    {
        var trimmed = text.TrimStart();
        return trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase);
    }

    private static string StripStringLiterals(string sql)
    {
        var sb = new System.Text.StringBuilder(sql.Length);
        var i = 0;
        while (i < sql.Length)
        {
            if (sql[i] is '\'' or '"')
            {
                var quote = sql[i];
                i++;
                while (i < sql.Length)
                {
                    if (sql[i] is '\\' && i + 1 < sql.Length)
                    {
                        i += 2;
                        continue;
                    }

                    if (sql[i] == quote)
                    {
                        if (i + 1 < sql.Length && sql[i + 1] == quote)
                        {
                            i += 2;
                            continue;
                        }

                        i++;
                        break;
                    }

                    i++;
                }

                sb.Append(' ');
                continue;
            }

            if (i + 1 < sql.Length && sql[i] is 'N' or 'n' && sql[i + 1] is '\'')
            {
                i += 2;
                while (i < sql.Length)
                {
                    if (sql[i] == '\'' && i + 1 < sql.Length && sql[i + 1] == '\'')
                    {
                        i += 2;
                        continue;
                    }

                    if (sql[i] == '\'')
                    {
                        i++;
                        break;
                    }

                    i++;
                }

                sb.Append(' ');
                continue;
            }

            sb.Append(sql[i]);
            i++;
        }

        return sb.ToString();
    }

    [GeneratedRegex(@"^[\w]+(\.[\w]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ViewReferencePattern();

    [GeneratedRegex(@"/\*", RegexOptions.CultureInvariant)]
    private static partial Regex BlockCommentPattern();

    [GeneratedRegex(@"--", RegexOptions.CultureInvariant)]
    private static partial Regex LineCommentPattern();

    [GeneratedRegex(@"\bSELECT\b[\s\S]*\bINTO\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SelectIntoPattern();

    [GeneratedRegex(@"\b(OPENROWSET|OPENDATASOURCE|xp_\w+|sp_executesql|BULK\s+INSERT)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DangerousTokenPattern();

    private static bool ContainsWholeWord(string text, string word) =>
        Regex.IsMatch(
            text,
            $@"\b{Regex.Escape(word)}\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}