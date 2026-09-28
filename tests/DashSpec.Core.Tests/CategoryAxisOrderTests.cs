using DashSpec.Core.Model;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class CategoryAxisOrderTests
{
    [Fact]
    public void Sort_by_value_desc_is_default_without_y_format()
    {
        var labels = new List<string> { "a", "b", "c" };
        var totals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = 1,
            ["b"] = 3,
            ["c"] = 2,
        };

        CategoryAxisOrdering.Sort(labels, totals, new Dictionary<string, string>());

        Assert.Equal(["b", "c", "a"], labels);
    }

    [Fact]
    public void Sort_by_label_asc_when_specified()
    {
        var labels = new List<string> { "z", "a", "m" };
        var totals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["y_sort"] = "label",
            ["y_order"] = "asc",
        };

        CategoryAxisOrdering.Sort(labels, totals, props);

        Assert.Equal(["a", "m", "z"], labels);
    }

    [Fact]
    public void Sort_raw_y_format_defaults_to_label_asc_without_y_sort()
    {
        var labels = new List<string> { "NanoCAD", "AutoCAD", "LIRA" };
        var totals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["NanoCAD"] = 100,
            ["AutoCAD"] = 1,
            ["LIRA"] = 50,
        };
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["y_format"] = "raw",
        };

        CategoryAxisOrdering.Sort(labels, totals, props);

        Assert.Equal(["AutoCAD", "LIRA", "NanoCAD"], labels);
    }

    [Fact]
    public void ResolveSortMode_defaults_to_value_when_y_format_omitted()
    {
        Assert.Equal(
            CategorySortMode.Value,
            CategoryAxisOrderParser.ResolveSortMode(new Dictionary<string, string>(), CategoryAxis.Y));
    }

    [Fact]
    public void ResolveSortMode_raw_is_label()
    {
        var props = new Dictionary<string, string> { ["y_format"] = "raw" };
        Assert.Equal(CategorySortMode.Label, CategoryAxisOrderParser.ResolveSortMode(props, CategoryAxis.Y));
    }
}
