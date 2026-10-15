using DashSpec.Core.Parsing;
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

    [Fact]
    public void EnsureUniqueModuleId_throws_on_duplicate()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ImportMergePolicy.EnsureUniqueModuleId("sales", "first.ds", seen);
        var ex = Assert.Throws<DashSpecParseException>(() =>
            ImportMergePolicy.EnsureUniqueModuleId("sales", "second.ds", seen));
        Assert.Contains("sales", ex.Message, StringComparison.Ordinal);
        Assert.Contains("second.ds", ex.Message, StringComparison.Ordinal);
    }
}
