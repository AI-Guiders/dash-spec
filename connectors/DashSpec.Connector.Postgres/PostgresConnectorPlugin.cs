using DashSpec.Abstractions.Connectors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Connector.Postgres;

public sealed class PostgresConnectorPlugin : IConnectorPlugin
{
    public string Id => "postgres";

    public string DisplayName => "PostgreSQL";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PostgresConnectorOptions>(
            configuration.GetSection(PostgresConnectorOptions.SectionName));

        services.AddSingleton<IDataSourceConnector, PostgresConnector>();
    }
}
