using Xunit;

namespace DashSpec.Analyzers.Tests;

public sealed class DashSpecLayerPathsTests
{
    [Theory]
    [InlineData(@"D:\open\dash-spec\connectors\DashSpec.Connector.SqlServer\X.cs", true)]
    [InlineData(@"D:\open\dash-spec\src\DashSpec.Execution.Runtime\X.cs", false)]
    public void IsConnectorAcquisitionLayer(string path, bool expected) =>
        Assert.Equal(expected, DashSpecLayerPaths.IsConnectorAcquisitionLayer(path));

    [Theory]
    [InlineData(@"D:\open\dash-spec\src\DashSpec.Execution.Runtime\X.cs", true)]
    [InlineData(@"D:\open\dash-spec\src\DashSpec.Core\X.cs", false)]
    public void IsExecutionRuntimeLayer(string path, bool expected) =>
        Assert.Equal(expected, DashSpecLayerPaths.IsExecutionRuntimeLayer(path));

    [Theory]
    [InlineData(@"D:\open\dash-spec\src\DashSpec.Execution.Runtime\X.cs", true)]
    [InlineData(@"D:\open\dash-spec\connectors\DashSpec.Connector.SqlServer\X.cs", false)]
    public void IsUntypedRowBagForbiddenLayer(string path, bool expected) =>
        Assert.Equal(expected, DashSpecLayerPaths.IsUntypedRowBagForbiddenLayer(path));
}
