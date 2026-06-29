using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Abstractions.Connectors;

/// <summary>Forge-style plugin entry: registers an <see cref="IDataSourceConnector"/> in DI.</summary>
public interface IConnectorPlugin
{
    string Id { get; }

    string DisplayName { get; }

    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
}
