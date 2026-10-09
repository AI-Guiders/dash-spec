using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Execution.Compilation;
using Xunit;

namespace DashSpec.Core.Tests;

public class ToolbarFilterVisibilityTests
{
    [Fact]
    public void Page_toolbar_chart_top_visible_not_on_card_local_filters()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @tab customer_reports
                  !include "query-row-types.dashtype"
              report
              title = "Customer"
              defaults
                filter.usage_date.range = -7d..today
                filter.chart_top.limit = 200
              end defaults
              filter usage_date
                bind date
                  column = usage_date
                end bind
                show
                  label = "Report date"
                end show
              end filter
              filter chart_top
                bind top
                end bind
                show
                  label = "Rows (TOP)"
                end show
              end filter
              page peak_util
                show flow
                  usage_date -> [toolbar] chrome.page.peak_util
                  chart_top -> [toolbar] chrome.page.peak_util
                end show flow
                card peak_by_app as "Peak by app"
                  diagram bar
                  category = app_name
                  value = peak_concurrent_proxy
                  end bar
                  data flow { fixture_src [rows] -> [rows] __diagram__ }
                  bind usage_date, chart_top
                end card
              end page
              page executive_summary
                show flow
                  usage_date -> [toolbar] chrome.page.executive_summary
                  chart_top -> [toolbar] chrome.page.executive_summary
                end show flow
                card exec_top_chart as "Top chart"
                  diagram bar
                  category = app_name
                  value = peak_concurrent_proxy
                  end bar
                  data flow { fixture_src [rows] -> [rows] __diagram__ }
                  bind usage_date, chart_top
                end card
              end page
              end report
            end tab
            """);

        var map = FilterBinding.MapFiltersToCards(doc);

        AssertPageToolbarHasChartTop(doc, "peak_util");
        AssertPageToolbarHasChartTop(doc, "executive_summary");
        AssertCardHasNoLocalChartTop(doc, "peak_by_app");
        AssertCardHasNoLocalChartTop(doc, "exec_top_chart");

        Assert.Contains("chart_top", map);
        Assert.Contains("exec_top_chart", map["chart_top"]);

        var peakVisible = ToolbarFilterVisibility.ResolveVisibleFilters(
            doc,
            activeTabId: "customer_reports",
            activePageId: "peak_util",
            map);
        Assert.Contains("chart_top", peakVisible);

        var execVisible = ToolbarFilterVisibility.ResolveVisibleFilters(
            doc,
            activeTabId: "customer_reports",
            activePageId: "executive_summary",
            map);
        Assert.Contains("chart_top", execVisible);
    }

    private static void AssertPageToolbarHasChartTop(DashboardDocument doc, string pageId)
    {
        var page = doc.Pages!.Single(p => string.Equals(p.Id, pageId, StringComparison.OrdinalIgnoreCase));
        var toolbar = page.ToolbarBoard!.Rows.SelectMany(row => row).ToList();
        Assert.Contains("chart_top", toolbar);
    }

    private static void AssertCardHasNoLocalChartTop(DashboardDocument doc, string cardId)
    {
        var card = doc.Cards.Single(c => string.Equals(c.Id, cardId, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("chart_top", card.LocalFilters);
    }
}
