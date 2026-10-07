using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace DashSpec.Architecture.Tests;

public sealed class LayerDependencyTests
{
    private static readonly Assembly HostAssembly = typeof(DashSpec.Host.Configuration.HostServiceCollectionExtensions).Assembly;
    private static readonly Assembly PresentationAssembly = typeof(DashSpec.Presentation.Filters.FilterLargeListOptions).Assembly;
    private static readonly Assembly RuntimeAssembly = typeof(DashSpec.Execution.Runtime.LabelFormat).Assembly;
    private static readonly Assembly VizAssembly = typeof(DashSpec.Viz.CardRenderResult).Assembly;
    private static readonly Assembly FiltersAssembly = typeof(DashSpec.Filters.FilterWidgetRenderContext).Assembly;
    private static readonly Assembly CompilationAssembly = typeof(DashSpec.Execution.Compilation.ReportCompiler).Assembly;
    private static readonly Assembly SurfaceBlazorAssembly = typeof(DashSpec.Surface.Blazor.Configuration.ViewerSurfaceServiceCollectionExtensions).Assembly;
    private static readonly Assembly ViewerAssembly = typeof(DashSpec.Viewer.ViewerPluginHost).Assembly;
    private static readonly Assembly CoreAssembly = typeof(DashSpec.Core.Platform.ReportLoadOptions).Assembly;

    [Fact]
    public void Host_must_not_reference_Presentation()
    {
        var result = Types.InAssembly(HostAssembly)
            .ShouldNot()
            .HaveDependencyOn("DashSpec.Presentation")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join("; ", result.FailingTypeNames ?? []));
    }

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

    [Fact]
    public void Execution_Runtime_must_not_reference_SqlClient()
    {
        var result = Types.InAssembly(RuntimeAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.Data.SqlClient")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join("; ", result.FailingTypeNames ?? []));
    }
    [Fact]
    public void Execution_Compilation_must_not_reference_AspNetCore()
    {
        var result = Types.InAssembly(CompilationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join("; ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Core_must_not_reference_AspNetCore()
    {
        var result = Types.InAssembly(CoreAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join("; ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Execution_Runtime_must_not_reference_AspNetCore()
    {
        var result = Types.InAssembly(RuntimeAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join("; ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Surface_Blazor_must_not_reference_Host()
    {
        var result = Types.InAssembly(SurfaceBlazorAssembly)
            .ShouldNot()
            .HaveDependencyOn("DashSpec.Host")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join("; ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Viewer_must_not_reference_Host()
    {
        var result = Types.InAssembly(ViewerAssembly)
            .ShouldNot()
            .HaveDependencyOn("DashSpec.Host")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join("; ", result.FailingTypeNames ?? []));
    }
}
