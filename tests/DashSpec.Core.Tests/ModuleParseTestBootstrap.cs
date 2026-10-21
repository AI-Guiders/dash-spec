using System.Runtime.CompilerServices;

namespace DashSpec.Core.Tests;

internal static class ModuleParseTestBootstrap
{
    [ModuleInitializer]
    internal static void Init()
    {
        DashSpec.Execution.Parsing.DashSpecParser.EnsureModuleParsersRegistered();

        // The host default grid stand-in ([ADR-0111] §2): in unit tests there is no host, so the
        // embedded host.ds drawing the host would register is registered here directly — the grid
        // default lives in ds (host.ds in the host, this stand-in in tests), never in engine code.
        DashSpec.Modeling.Parse.Layout.GridDefaultBridge.Default =
            Microsoft.FSharp.Core.FSharpOption<DashSpec.Modeling.Parse.Layout.LayoutDefinition>.Some(
                new DashSpec.Modeling.Parse.Layout.LayoutDefinition { Columns = 12, GapPx = 16 });
    }
}
