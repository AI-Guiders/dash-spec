namespace DashSpec.Core.Catalog;

/// <summary>Current catalog bootstrap — updated on git pull.</summary>
public sealed class CatalogSourceState
{
    private CatalogBootstrap _bootstrap;

    public CatalogSourceState(CatalogBootstrap bootstrap) => _bootstrap = bootstrap;

    public CatalogBootstrap Current => _bootstrap;

    public void Replace(CatalogBootstrap bootstrap) => _bootstrap = bootstrap;
}
