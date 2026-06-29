using DashSpec.Abstractions.Connectors;
using DashSpec.Abstractions.Query;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace DashSpec.Connector.SqlServer;

public sealed class SqlServerConnector(IOptions<SqlServerConnectorOptions> options) : IDataSourceConnector
{
    public string Id => "sqlserver";

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        CompiledQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(options.Value.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new SqlCommand(query.Sql, connection);
        foreach (var parameter in query.Parameters)
        {
            command.Parameters.AddWithValue(NormalizeParameterName(parameter.Name), CoerceValue(parameter.Value));
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
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

        await using var command = new SqlCommand(sql, connection);
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
}

public sealed class SqlServerConnectorOptions
{
    public const string SectionName = "Connectors:SqlServer";

    public string ConnectionString { get; set; } = string.Empty;
}
