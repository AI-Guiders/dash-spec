using DashSpec.Abstractions.Connectors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Connector.SqlServer;

public sealed class SqlServerConnectorPlugin : IConnectorPlugin
{
    public string Id => "sqlserver";

    public string DisplayName => "Microsoft SQL Server";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SqlServerConnectorOptions>(
            configuration.GetSection(SqlServerConnectorOptions.SectionName));

        services.AddSingleton<IDataSourceConnector, SqlServerConnector>();
    }
}
