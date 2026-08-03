using DashSpec.Abstractions.Connectors;
using DashSpec.Abstractions.Query;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace DashSpec.Connector.SqlServer;

public sealed class SqlServerConnector(IOptions<SqlServerConnectorOptions> options) : IDataSourceConnector
{
    private const int DefaultCommandTimeoutSeconds = 120;
    private const int DefaultMaxRows = 250_000;

    public string Id => "sqlserver";

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        CompiledQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(options.Value.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new SqlCommand(query.Sql, connection)
        {
            CommandTimeout = ResolveCommandTimeout(),
        };
        foreach (var parameter in query.Parameters)
        {
            command.Parameters.AddWithValue(NormalizeParameterName(parameter.Name), CoerceValue(parameter.Value));
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        var maxRows = ResolveMaxRows();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (rows.Count >= maxRows)
            {
                throw new InvalidOperationException(
                    $"SQL result exceeded max_rows ({maxRows}). Narrow date/product filters or raise [connectors.sqlserver] max_rows.");
            }

            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            rows.Add(row);
        }

        return rows;
    }

    public async Task<IReadOnlyList<string>> QueryDistinctStringsAsync(
        string sql,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(options.Value.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = ResolveCommandTimeout(),
        };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var values = new List<string>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!reader.IsDBNull(0))
            {
                values.Add(Convert.ToString(reader.GetValue(0)) ?? string.Empty);
            }
        }

        return values;
    }

    private static string NormalizeParameterName(string name) =>
        name.StartsWith('@') ? name : "@" + name;

    private static object CoerceValue(object value) =>
        value switch
        {
            DateOnly date => date.ToDateTime(TimeOnly.MinValue),
            _ => value,
        };

    private int ResolveCommandTimeout() =>
        options.Value.CommandTimeoutSeconds > 0
            ? options.Value.CommandTimeoutSeconds
            : DefaultCommandTimeoutSeconds;

    private int ResolveMaxRows() =>
        options.Value.MaxRows > 0
            ? options.Value.MaxRows
            : DefaultMaxRows;
}

public sealed class SqlServerConnectorOptions
{
    public const string SectionName = "Connectors:SqlServer";

    public string ConnectionString { get; set; } = string.Empty;

    public int CommandTimeoutSeconds { get; set; }

    public int MaxRows { get; set; }
}
