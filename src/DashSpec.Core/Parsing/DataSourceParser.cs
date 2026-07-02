using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class DataSourceParser
{
    public static DataSourceDefinition Parse(TokenReader reader, string? specDirectory = null)
    {
        if (reader.TryKeyword("view"))
        {
            var name = reader.ReadQualifiedName();
            SqlReadOnlyValidator.ValidateViewReference(name);
            return new DataSourceDefinition(DataSourceKind.View, name);
        }

        if (reader.TryKeyword("sql"))
        {
            if (reader.IsAt(TokenKind.LBrace))
            {
                return ParseSqlBlock(reader, specDirectory);
            }

            return ParseSqlInline(reader, specDirectory);
        }

        throw reader.Unexpected("view or sql");
    }

    private static DataSourceDefinition ParseSqlInline(TokenReader reader, string? specDirectory)
    {
        if (reader.TryKeyword("query"))
        {
            var body = ReadSqlQueryText(reader);
            SqlReadOnlyValidator.ValidateSqlBody(body);
            return new DataSourceDefinition(DataSourceKind.Sql, body, DataSourceSqlCarrier.Query);
        }

        if (reader.TryKeyword("file"))
        {
            var path = ReadSqlFileReference(reader);
            ValidateSqlFileExists(path, specDirectory);
            return new DataSourceDefinition(DataSourceKind.Sql, path, DataSourceSqlCarrier.File);
        }

        throw new DashSpecParseException(
            "datasource sql requires 'query' or 'file' (e.g. datasource sql query \"SELECT …\" or datasource sql file \"sql/x.sql\").");
    }

    private static DataSourceDefinition ParseSqlBlock(TokenReader reader, string? specDirectory)
    {
        reader.Expect(TokenKind.LBrace);
        reader.SkipNewlines();

        DataSourceDefinition? parsed = null;

        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.IsAt(TokenKind.RBrace))
            {
                break;
            }

            if (!reader.TryKeyword("from"))
            {
                throw reader.Unexpected("from");
            }

            if (reader.TryKeyword("query"))
            {
                var body = ReadSqlQueryText(reader);
                SqlReadOnlyValidator.ValidateSqlBody(body);
                parsed = new DataSourceDefinition(DataSourceKind.Sql, body, DataSourceSqlCarrier.Query);
            }
            else if (reader.TryKeyword("file"))
            {
                var path = ReadSqlFileReference(reader);
                ValidateSqlFileExists(path, specDirectory);
                parsed = new DataSourceDefinition(DataSourceKind.Sql, path, DataSourceSqlCarrier.File);
            }
            else
            {
                throw reader.Unexpected("query or file after from");
            }

            reader.SkipNewlines();
        }

        reader.Expect(TokenKind.RBrace);

        if (parsed is null)
        {
            throw new DashSpecParseException("datasource sql { } requires from query or from file.");
        }

        return parsed;
    }

    private static string ReadSqlQueryText(TokenReader reader)
    {
        reader.SkipNewlines();
        return reader.CurrentKind switch
        {
            TokenKind.String => reader.ReadString(),
            TokenKind.Raw => UnwrapRawSql(reader.ReadRawBlock()),
            _ => throw reader.Unexpected("SQL query string or [[ … ]] block"),
        };
    }

    private static string ReadSqlFileReference(TokenReader reader)
    {
        reader.SkipNewlines();
        if (reader.CurrentKind is not TokenKind.String)
        {
            throw reader.Unexpected("file path string");
        }

        var path = reader.ReadString();
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new DashSpecParseException("datasource sql file path must not be empty.");
        }

        return path.Replace('\\', '/');
    }

    private static void ValidateSqlFileExists(string relativePath, string? specDirectory)
    {
        if (string.IsNullOrWhiteSpace(specDirectory))
        {
            return;
        }

        var path = Path.GetFullPath(Path.Combine(specDirectory, relativePath));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"datasource sql file not found: '{relativePath}' (resolved: {path}).",
                path);
        }
    }

    private static string UnwrapRawSql(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("[[", StringComparison.Ordinal) &&
            trimmed.EndsWith("]]", StringComparison.Ordinal))
        {
            return trimmed[2..^2].Trim();
        }

        return trimmed;
    }
}
