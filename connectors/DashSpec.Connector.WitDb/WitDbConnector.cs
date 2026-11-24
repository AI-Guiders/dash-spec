using DashSpec.Abstractions.Connectors;
using DashSpec.Abstractions.Data;
using DashSpec.Abstractions.Data.Acquisition;
using DashSpec.Abstractions.Query;
using Microsoft.Extensions.Options;
using OutWit.Database.AdoNet;

namespace DashSpec.Connector.WitDb;

public sealed class WitDbConnector(IOptions<WitDbConnectorOptions> options) : IDataSourceConnector
{
    private const int DefaultCommandTimeoutSeconds = 30;
    private const int DefaultMaxRows = 100_000;

    public string Id => "witdb";

    public async Task<TypedRowBatch> QueryAsync(
        CompiledQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new WitDbConnection(options.Value.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new WitDbCommand(query.Sql, connection)
        {
            CommandTimeout = ResolveCommandTimeout(),
        };

        foreach (var parameter in query.Parameters)
        {
            var witParam = command.CreateParameter();
            witParam.ParameterName = NormalizeParameterName(parameter.Name);
            witParam.DbType = parameter.ResolvedDbType;
            witParam.Value = parameter.Value;
            command.Parameters.Add(witParam);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var schema = query.RowSchema;
        var columnOrdinals = SqlRowMaterializer.ResolveColumnOrdinals(reader, schema);
        var rowValues = new List<DashValue[]>();
        var maxRows = ResolveMaxRows();

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (rowValues.Count >= maxRows)
            {
                throw new InvalidOperationException(
                    $"SQL result exceeded max_rows ({maxRows}). Narrow filters or raise [providers.witdb] max_rows.");
            }

            rowValues.Add(SqlRowMaterializer.ReadRow(reader, schema, columnOrdinals, query.DateTimeFrame));
        }

        return TypedRowBatch.Create(schema, rowValues);
    }

    public async Task<IReadOnlyList<string>> QueryDistinctStringsAsync(
        string sql,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new WitDbConnection(options.Value.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new WitDbCommand(sql, connection)
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

    private int ResolveCommandTimeout() =>
        options.Value.CommandTimeoutSeconds > 0
            ? options.Value.CommandTimeoutSeconds
            : DefaultCommandTimeoutSeconds;

    private int ResolveMaxRows() =>
        options.Value.MaxRows > 0
            ? options.Value.MaxRows
            : DefaultMaxRows;
}

public sealed class WitDbConnectorOptions
{
    public const string SectionName = "Providers:WitDb";

    public string ConnectionString { get; set; } = string.Empty;

    public int CommandTimeoutSeconds { get; set; }

    public int MaxRows { get; set; }
}
