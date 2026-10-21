namespace DashSpec.Modeling.Parse.Tests

module GridDefaultTestBootstrap =

    /// <summary>
    /// The host default grid stand-in ([ADR-0111] §2): no host in unit tests, so the embedded
    /// host.ds drawing the host would register is registered here directly — the grid default
    /// lives in ds, never in engine code. F# has no ModuleInitializer (FS0202), so test modules
    /// call <c>do GridDefaultTestBootstrap.ensure ()</c> at their top.
    /// </summary>
    let ensure () =
        DashSpec.Modeling.Parse.Layout.GridDefaultBridge.Default <-
            Some { DashSpec.Modeling.Parse.Layout.LayoutDefinition.Columns = 12
                   GapPx = 16 }
