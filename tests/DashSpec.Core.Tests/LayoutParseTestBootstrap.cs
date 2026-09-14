using System.Runtime.CompilerServices;

namespace DashSpec.Core.Tests;

internal static class LayoutParseTestBootstrap
{
    [ModuleInitializer]
    internal static void Init() => DashSpec.Execution.Parsing.DashSpecParser.EnsureModuleParsersRegistered();
}
