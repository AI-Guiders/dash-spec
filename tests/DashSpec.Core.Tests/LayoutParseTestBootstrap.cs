using System.Runtime.CompilerServices;

namespace DashSpec.Core.Tests;

internal static class LayoutParseTestBootstrap
{
    [ModuleInitializer]
    internal static void Init() => _ = typeof(DashSpec.Execution.Parsing.DashSpecParser);
}
