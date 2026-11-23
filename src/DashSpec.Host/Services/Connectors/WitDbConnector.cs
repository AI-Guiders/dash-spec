using DashSpec.Abstractions.Connectors;
using DashSpec.Abstractions.Data;
using DashSpec.Abstractions.Data.Acquisition;
using DashSpec.Abstractions.Query;
using OutWit.Database.AdoNet;

namespace DashSpec.Host.Services.Connectors;

/// <summary>
/// ADR-0137: Built-in WitDatabase (<see cref="WitDbConnection"/>) connector (<c>provider = "witdb"</c>)
/// enabling DashSpec reports to query local <c>.witdb</c> databases, including the host's own
/// telemetry and settings store.
/// </summary>
public sealed class WitDbConnector(string connectionString, int commandTimeoutSeconds = 30, int maxRows = 100_000) : IDataSourceConnector
{
    public string Id => "witdb";

    public async Task<TypedRowBatch> QueryAsync(
        CompiledQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new WitDbConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new WitDbCommand(query.Sql, connection)
        {
            CommandTimeout = commandTimeoutSeconds > 0 ? commandTimeoutSeconds : 30,
        };

        foreach (var parameter in query.Parameters)
        {
            var witParam = command.CreateParameter();
            witParam.ParameterName = parameter.Name.StartsWith('@') ? parameter.Name : "@" + parameter.Name;
            witParam.DbType = parameter.ResolvedDbType;
            witParam.Value = parameter.Value;
            command.Parameters.Add(witParam);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var schema = query.RowSchema;
        var columnOrdinals = SqlRowMaterializer.ResolveColumnOrdinals(reader, schema);
        var rowValues = new List<DashValue[]>();
        var limit = maxRows > 0 ? maxRows : 100_000;

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (rowValues.Count >= limit)
            {
                break;
            }

            rowValues.Add(SqlRowMaterializer.ReadRow(reader, schema, columnOrdinals, query.DateTimeFrame));
        }

        return TypedRowBatch.Create(schema, rowValues);
    }

    public async Task<IReadOnlyList<string>> QueryDistinctStringsAsync(
        string sql,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new WitDbConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new WitDbCommand(sql, connection)
        {
            CommandTimeout = commandTimeoutSeconds > 0 ? commandTimeoutSeconds : 30,
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
}
