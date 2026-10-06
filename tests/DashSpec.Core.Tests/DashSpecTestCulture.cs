using System.Globalization;
using System.Runtime.CompilerServices;
using DashSpec.Execution.Runtime;

namespace DashSpec.Core.Tests;

/// <summary>Host defaults to ru-RU; unit tests must match preset expectations (dd.MM, HH:mm).</summary>
internal static class DashSpecTestCulture
{
    [ModuleInitializer]
    internal static void Initialize() => Ensure();

    internal static void Ensure()
    {
        var ru = CultureInfo.GetCultureInfo("ru-RU");
        CultureInfo.DefaultThreadCurrentCulture = ru;
        CultureInfo.DefaultThreadCurrentUICulture = ru;
        CultureInfo.CurrentCulture = ru;
        CultureInfo.CurrentUICulture = ru;
        LabelFormat.UiCulture = ru;
    }
}
