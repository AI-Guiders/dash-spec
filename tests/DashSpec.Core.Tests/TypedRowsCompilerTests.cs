using System.Reflection;
using System.Runtime.Loader;
using DashSpec.Execution.Compilation;
using DashSpec.Modeling.Core;
using Xunit;

namespace DashSpec.Core.Tests;

/// <summary>ADR-0124: dsc build types compiles row types into a real CLR assembly —
/// POCO with typed properties + RowTypeRegistry (name → factory), loadable in one lookup.</summary>
public class TypedRowsCompilerTests
{
    private static RowTypeDef Def(string name, params (string Field, DashPrimitive Kind)[] fields) =>
        new(
            name,
            fields.Select(f => new RowFieldDef(f.Field, DashType.NewPrimitive(f.Kind), true)).ToArray());

    private static (string Dll, object Instance) BuildAndLoad()
    {
        var dir = Path.Combine(Path.GetTempPath(), "typedrows-" + Guid.NewGuid().ToString("N"));
        var result = TypedRowsCompiler.Compile(
            [Def("SampleRow", ("usage_date", DashPrimitive.Date), ("program", DashPrimitive.String), ("cnt", DashPrimitive.Int))],
            dir,
            "TypedRows.Tests");

        var alc = new AssemblyLoadContext("typedrows-test", isCollectible: false);
        var assembly = alc.LoadFromAssemblyPath(result.AssemblyPath);
        var registry = assembly.GetType("DashSpec.TypedRows.RowTypeRegistry")!;
        var contains = (bool)registry.InvokeMember(
            "Contains", BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod, null, null, ["SampleRow"])!;
        var instance = registry.InvokeMember(
            "Create", BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod, null, null,
            [
                "SampleRow",
                new[]
                {
                    DashSpec.Abstractions.Data.DashValue.FromDate(new DateOnly(2026, 11, 8)),
                    DashSpec.Abstractions.Data.DashValue.FromString("DESIGN"),
                    DashSpec.Abstractions.Data.DashValue.FromInt(42),
                },
                new[] { "usage_date", "program", "cnt" },
            ])!;
        return (result.AssemblyPath, instance);
    }

    [Fact]
    public void Compiles_and_creates_typed_rows_from_the_wire()
    {
        var (dll, instance) = BuildAndLoad();
        Assert.True(File.Exists(dll));
        Assert.Equal("DESIGN", instance.GetType().GetProperty("program")!.GetValue(instance));
        Assert.Equal(42, instance.GetType().GetProperty("cnt")!.GetValue(instance));
        Assert.Equal(new DateOnly(2026, 11, 8), instance.GetType().GetProperty("usage_date")!.GetValue(instance));
    }
}
