using DashSpec.Host.Configuration;

namespace DashSpec.Host.Services.Abstractions;

public interface IHostDatabaseInitializer
{
    string ResolveDatabasePath(DashSpecTomlRoot bootstrap);

    void EnsureDatabase(string databasePath);
}
