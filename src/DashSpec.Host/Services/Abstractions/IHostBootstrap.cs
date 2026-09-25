using DashSpec.Host.Configuration;

namespace DashSpec.Host.Services.Abstractions;

public interface IHostBootstrap
{
    DashSpecTomlRoot LoadBootstrap(IHostEnvironment environment);

    (DashSpecTomlRoot Bootstrap, HostShellBootstrap HostShell) LoadBootstrapWithHost(IHostEnvironment environment);

    CatalogBootstrap LoadCatalog(DashSpecTomlRoot bootstrap, string contentRoot);

    string ResolveActiveSpecFullPath(CatalogBootstrap catalog, string? catalogEntryId = null);

    DashSpecTomlRoot Load(IHostEnvironment environment);

    DashSpecAccessOptions LoadAccessOptions(IHostEnvironment environment);
}
