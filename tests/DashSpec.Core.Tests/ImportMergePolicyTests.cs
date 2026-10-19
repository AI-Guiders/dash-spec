using DashSpec.Execution.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class ImportMergePolicyTests
{
    [Fact]
    public void OrderGlobPaths_sorts_ordinal_ignore_case()
    {
        var ordered = ImportMergePolicy.OrderGlobPaths(
            ["b.ds", "A.ds", "a.ds"]);
        Assert.Equal(["A.ds", "a.ds", "b.ds"], ordered);
    }
}
