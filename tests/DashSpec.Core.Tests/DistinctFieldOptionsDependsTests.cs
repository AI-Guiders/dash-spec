namespace DashSpec.Core.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DashSpec.Execution.Compilation;
using DashSpec.Execution.Runtime;
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

    [Fact]
    public void card_field_domain_query_scopes_by_card_period_and_excludes_self_selection()
    {
        var soakPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "samples", "demo",
            "demo-soak.ds"));
        var specDir = Path.GetDirectoryName(soakPath)!;
        var doc = DashSpecTestRowTypes.ParseDashboard(File.ReadAllText(soakPath), specDir);
        var filterIndex = DashboardBootstrap.IndexFilters(doc);
        var filters = DashboardBootstrap.CreateInitialFilters(doc, new DateOnly(2026, 11, 18));
        filters.SetField("app_name", ["ONLY_THIS_APP"]);

        var rawCard = doc.Cards.Single(c => c.Id == "peak_concurrent_proxy");
        var resolved = CardResolver.Resolve(rawCard, null, doc.DashboardFilters).Card;
        var effective = DocumentFlowBinder.MaterializeCard(resolved, doc);

        var domainQuery = QueryCompiler.TryCompileCardFieldDomainQuery(
            effective,
            "app_name",
            filters,
            filterIndex,
            doc,
            doc.SqlDialect,
            specDir);

        Assert.NotNull(domainQuery);
        Assert.Contains("SELECT DISTINCT app_name AS app_name", domainQuery!.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("usage_date =", domainQuery.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("app_name =", domainQuery.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("app_name IN", domainQuery.Sql, StringComparison.OrdinalIgnoreCase);
    }
}
