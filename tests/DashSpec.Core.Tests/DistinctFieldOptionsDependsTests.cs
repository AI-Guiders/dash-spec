namespace DashSpec.Core.Tests;

using System;
using System.Collections.Generic;
using DashSpec.Execution.Compilation;
using Xunit;

public class DistinctFieldOptionsDependsTests
{
    private static readonly IReadOnlyDictionary<string, FilterDefinition> Definitions =
        new Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["platform"] = FilterTestData.Create(FilterKind.Field, "platform", null, "v_activity.platform"),
            ["product"] = FilterTestData.Create(FilterKind.Field, "product", null, "v_activity.product", dependsOn: "platform"),
            ["report_scoped"] = FilterTestData.Create(FilterKind.Field, "report_scoped", null, "v_activity.product", dependsOn: "report.platform"),
        };

    [Fact]
    public void depends_selection_narrows_the_domain_with_an_in_predicate()
    {
        var sql = QueryCompiler.BuildDistinctFieldSql(Definitions["product"], Definitions, ["Windows", "Linux"]);

        Assert.Contains("v_activity.platform IN ('Windows', 'Linux')", sql);
        Assert.StartsWith("SELECT DISTINCT product AS value FROM v_activity", sql);
    }

    [Fact]
    public void empty_selection_keeps_the_full_domain()
    {
        var sql = QueryCompiler.BuildDistinctFieldSql(Definitions["product"], Definitions, []);

        Assert.DoesNotContain("IN (", sql);
    }

    [Fact]
    public void report_prefix_resolves_against_the_report_dictionary()
    {
        var sql = QueryCompiler.BuildDistinctFieldSql(Definitions["report_scoped"], Definitions, ["Linux"]);

        Assert.Contains("v_activity.platform IN ('Linux')", sql);
    }

    [Fact]
    public void selection_values_are_escaped()
    {
        var sql = QueryCompiler.BuildDistinctFieldSql(Definitions["product"], Definitions, ["O'Brien"]);

        Assert.Contains("'O''Brien'", sql);
    }

    [Fact]
    public void unknown_depends_target_is_a_loud_error()
    {
        var ghost = FilterTestData.Create(FilterKind.Field, "ghost", null, "v_activity.product", dependsOn: "nowhere");

        var ex = Assert.Throws<ArgumentException>(
            () => QueryCompiler.BuildDistinctFieldSql(ghost, Definitions));
        Assert.Contains("depends on 'nowhere'", ex.Message);
    }

    [Fact]
    public void non_field_depends_target_is_a_loud_error()
    {
        var withDate = new Dictionary<string, FilterDefinition>(Definitions, StringComparer.OrdinalIgnoreCase)
        {
            ["usage_date"] = FilterTestData.Create(FilterKind.Date, "usage_date", "-7d..today", "usage_date"),
            ["by_date"] = FilterTestData.Create(FilterKind.Field, "by_date", null, "v_activity.product", dependsOn: "usage_date"),
        };

        var ex = Assert.Throws<ArgumentException>(
            () => QueryCompiler.BuildDistinctFieldSql(withDate["by_date"], withDate));
        Assert.Contains("not a field filter", ex.Message);
    }
}
