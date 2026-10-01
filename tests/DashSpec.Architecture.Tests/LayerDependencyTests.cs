using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace DashSpec.Architecture.Tests;

public sealed class LayerDependencyTests
{
    private static readonly Assembly HostAssembly = typeof(DashSpec.Host.Services.Presentation.DashboardPageController).Assembly;
    private static readonly Assembly RuntimeAssembly = typeof(DashSpec.Execution.Runtime.LabelFormat).Assembly;
    private static readonly Assembly VizAssembly = typeof(DashSpec.Viz.CardRenderResult).Assembly;
    private static readonly Assembly FiltersAssembly = typeof(DashSpec.Filters.FilterWidgetRenderContext).Assembly;

    [Fact]
    public void Host_must_not_reference_Modeling_Parse()
    {
        var result = Types.InAssembly(HostAssembly)
            .ShouldNot()
            .HaveDependencyOn("DashSpec.Modeling.Parse")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join("; ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Execution_Runtime_must_not_reference_Host()
    {
        var result = Types.InAssembly(RuntimeAssembly)
            .ShouldNot()
            .HaveDependencyOn("DashSpec.Host")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join("; ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Viz_must_not_reference_Host()
    {
        var result = Types.InAssembly(VizAssembly)
            .ShouldNot()
            .HaveDependencyOn("DashSpec.Host")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join("; ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Filters_must_not_reference_Host()
    {
        var result = Types.InAssembly(FiltersAssembly)
            .ShouldNot()
            .HaveDependencyOn("DashSpec.Host")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join("; ", result.FailingTypeNames ?? []));
    }
}
