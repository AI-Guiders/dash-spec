using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class IncludeMergePolicyTests
{
    [Fact]
    public void OrderGlobPaths_sorts_ordinal_ignore_case()
    {
        var ordered = IncludeMergePolicy.OrderGlobPaths(
            ["b.dashdiagram", "A.dashdiagram", "a.dashdiagram"]);
        Assert.Equal(["A.dashdiagram", "a.dashdiagram", "b.dashdiagram"], ordered);
    }

    [Fact]
    public void EnsureUniqueModuleId_throws_on_duplicate()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IncludeMergePolicy.EnsureUniqueModuleId("sales", "first.dashdiagram", seen);
        var ex = Assert.Throws<DashSpecParseException>(() =>
            IncludeMergePolicy.EnsureUniqueModuleId("sales", "second.dashdiagram", seen));
        Assert.Contains("sales", ex.Message, StringComparison.Ordinal);
        Assert.Contains("second.dashdiagram", ex.Message, StringComparison.Ordinal);
    }
}
