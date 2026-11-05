using System.Runtime.CompilerServices;

// CA2255: module initializers belong to app code — this is the deliberate exception:
// the stdlib self-registers on assembly load so every parse path sees DashSpec.Std.
#pragma warning disable CA2255

namespace DashSpec.Execution.Parsing;

/// <summary>Registers the embedded stdlib the moment this assembly loads (ADR-0017):
/// parse flows that never touch <c>EnsureModuleParsersRegistered</c> (unit tests, direct
/// parser use) still see <c>import … from DashSpec.Std</c> — same availability the old
/// filesystem walk gave, minus the walking.</summary>
internal static class StdlibEmbeddedInitializer
{
    [ModuleInitializer]
    internal static void Register() => ModuleParseRegistration.EnsureStdlibRegistered();
}
