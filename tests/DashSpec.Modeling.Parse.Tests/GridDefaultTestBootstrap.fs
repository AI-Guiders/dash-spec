namespace DashSpec.Modeling.Parse.Tests

module GridDefaultTestBootstrap =

    /// <summary>
    /// The host default grid stand-in ([ADR-0111] §2): no host in unit tests, so the embedded
    /// host.ds drawing the host would register is registered here directly — the grid default
    /// lives in ds, never in engine code. A collection fixture: `do` in a module does not cover
    /// nested modules' tests (each module has its own static constructor), the fixture runs
    /// before every test of the "GridDefault" collection.
    /// </summary>
    type GridDefaultFixture() =
        do
            DashSpec.Modeling.Parse.Layout.GridDefaultBridge.Default <-
                Some { DashSpec.Modeling.Parse.Layout.LayoutDefinition.Columns = 12 }

    [<Xunit.CollectionDefinition("GridDefault")>]
    type GridDefaultCollection() =
        interface Xunit.ICollectionFixture<GridDefaultFixture>
