using DashSpec.Abstractions.Connectors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Connector.WitDb;

public sealed class WitDbConnectorPlugin : IConnectorPlugin
{
    public string Id => "witdb";

    public string DisplayName => "OutWit WitDatabase";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WitDbConnectorOptions>(
            configuration.GetSection(WitDbConnectorOptions.SectionName));

        services.AddSingleton<IDataSourceConnector, WitDbConnector>();
    }
}
