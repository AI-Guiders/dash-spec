using Xunit;

namespace DashSpec.Analyzers.Tests;

public sealed class LogicalPathCompatTests
{
    [Theory]
    [InlineData(@"D:\repo\src\DashSpec.Execution.Runtime\X.cs", "src/DashSpec.Execution.Runtime")]
    [InlineData(@"/home/user/dash-spec/connectors/SqlServer/Foo.cs", "connectors")]
    public void ContainsLayerSegment_matches_platform_style_paths(string physical, string prefix)
    {
        Assert.True(LogicalPathCompat.ContainsLayerSegment(physical, prefix));
    }
}
