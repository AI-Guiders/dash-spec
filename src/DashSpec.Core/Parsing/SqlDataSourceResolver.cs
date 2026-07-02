using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class SqlDataSourceResolver
{
    public static string ResolveSqlBody(DataSourceDefinition source, string? specDirectory)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Kind is not DataSourceKind.Sql)
        {
            throw new InvalidOperationException(
                $"Expected sql datasource, got '{source.Kind}'.");
        }

        if (source.SqlCarrier is DataSourceSqlCarrier.File)
        {
            if (string.IsNullOrWhiteSpace(specDirectory))
            {
                throw new DashSpecParseException(
                    "datasource sql file requires spec directory when resolving SQL.");
            }

            var path = Path.GetFullPath(Path.Combine(specDirectory, source.Value));
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"datasource sql file not found: '{source.Value}' (resolved: {path}).",
                    path);
            }

            var body = File.ReadAllText(path);
            SqlReadOnlyValidator.ValidateSqlBody(body);
            return body;
        }

        if (string.IsNullOrWhiteSpace(source.Value))
        {
            throw new DashSpecParseException("datasource sql query must not be empty.");
        }

        SqlReadOnlyValidator.ValidateSqlBody(source.Value);
        return source.Value;
    }
}
