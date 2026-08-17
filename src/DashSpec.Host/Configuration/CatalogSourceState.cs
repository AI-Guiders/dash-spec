namespace DashSpec.Host.Configuration;

/// <summary>Текущий catalog bootstrap — обновляется при git pull.</summary>
public sealed class CatalogSourceState
{
    private CatalogBootstrap _bootstrap;

    public CatalogSourceState(CatalogBootstrap bootstrap) => _bootstrap = bootstrap;

    public CatalogBootstrap Current => _bootstrap;

    public void Replace(CatalogBootstrap bootstrap) => _bootstrap = bootstrap;
}
