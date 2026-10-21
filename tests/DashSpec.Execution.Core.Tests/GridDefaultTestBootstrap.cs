using System.Runtime.CompilerServices;

namespace DashSpec.Execution.Core.Tests;

internal static class GridDefaultTestBootstrap
{
    [ModuleInitializer]
    internal static void Init() =>
        DashSpec.Modeling.Parse.Layout.GridDefaultBridge.Default =
            Microsoft.FSharp.Core.FSharpOption<DashSpec.Modeling.Parse.Layout.LayoutDefinition>.Some(
                new DashSpec.Modeling.Parse.Layout.LayoutDefinition { Columns = 12, GapPx = 16 });
}
