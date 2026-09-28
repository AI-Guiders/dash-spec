using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class MatrixYOrderParserTests
{
    [Fact]
    public void SortYLabels_by_value_desc_is_default()
    {
        var labels = new List<string> { "a", "b", "c" };
        var totals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = 1,
            ["b"] = 3,
            ["c"] = 2,
        };

        MatrixYOrderParser.SortYLabels(labels, totals, new Dictionary<string, string>());

        Assert.Equal(["b", "c", "a"], labels);
    }

    [Fact]
    public void SortYLabels_by_label_asc()
    {
        var labels = new List<string> { "z", "a", "m" };
        var totals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["y_sort"] = "label",
            ["y_order"] = "asc",
        };

        MatrixYOrderParser.SortYLabels(labels, totals, props);

        Assert.Equal(["a", "m", "z"], labels);
    }
}
