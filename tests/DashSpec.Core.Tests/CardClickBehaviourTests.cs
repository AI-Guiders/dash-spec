using Xunit;

using DashSpec.Execution.Runtime;

namespace DashSpec.Core.Tests;

public class CardClickBehaviourTests
{
    [Fact]
    public void Parse_on_click_show_list_from_tooltip_copy()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module section demo
              report
              title = "demo"
              standalone
              defaults
                filter.usage_date.range = -7d..today
              end defaults
              filter usage_date
              bind date
                column = usage_date
              end bind
              show
                label = "Date"
              end show
            end filter
              toolbar usage_date
              end standalone
              card peak_apps as "Peak apps"
              on click
              show below as list from tooltip copy
              end click
              tooltip peak_apps
              source = peak_apps
              end tooltip
              diagram heatmap
              x = usage_date
              y = user_sam
              value = peak_concurrent_apps
              end heatmap
              inspect
              use tooltip peak_apps
              as list
              end inspect
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              bind
                usage_date
              end bind
              end card
              end report
            end section
            """);

        var card = doc.Cards[0];
        Assert.NotNull(card.ClickBehaviour);
        var showEffect = card.ClickBehaviour!.Value.Effects[0];
        var showFields = CardClickEffectTestData.Fields(showEffect);
        Assert.Equal("ShowSelection", CardClickEffectTestData.CaseName(showEffect));
        Assert.Equal(nameof(ShowPlacement.Below), showFields[0]?.ToString());
        Assert.Equal(nameof(ShowFormat.List), showFields[1]?.ToString());
        Assert.Equal(nameof(ShowSource.Tooltip), showFields[2]?.ToString());
        Assert.Equal(true, showFields[3]);
        Assert.NotNull(card.Tooltip);
    }

    [Fact]
    public void Parse_on_click_goto_and_set_filters()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module section t
              report
              title = "T"
              standalone
              defaults
                filter.usage_date.range = -7d..today
              end defaults
              filter usage_date
              bind date
                column = usage_date
              end bind
              show
                label = "Date"
              end show
            end filter
              filter user_name
                bind field
                  column = dbo.t.user
                end bind
                show
                  label = "User"
                  widget = combobox
                end show
              end filter
              toolbar usage_date, user_name
              end standalone
              card peak_apps as "Peak"
              on click
              set usage_date from x
              set user_name from y
              goto section detail
              end click
              diagram heatmap
              x = a y
              value = c
              end heatmap
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end section
            """);

        var card = doc.Cards[0];
        Assert.NotNull(card.ClickBehaviour);
        Assert.Equal(3, card.ClickBehaviour!.Value.Effects.Count);
        Assert.Equal("SetFilterFromField", CardClickEffectTestData.CaseName(card.ClickBehaviour!.Value.Effects[0]));
        Assert.Equal("SetFilterFromField", CardClickEffectTestData.CaseName(card.ClickBehaviour.Value.Effects[1]));
        Assert.Equal("GotoTab", CardClickEffectTestData.CaseName(card.ClickBehaviour.Value.Effects[2]));
    }

    [Fact]
    public void Parse_on_click_drill_table_from_cell_binds_filters()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module section t
              report
              title = "T"
              defaults
                filter.usage_date.range = -7d..today
              end defaults
              filter usage_date
                bind date
                  column = usage_date
                end bind
                show
                  label = "Date"
                end show
              end filter
              filter app_name
                bind field
                  column = app_name
                end bind
                show
                  label = "App"
                end show
              end filter
              card peak as "Peak"
              on click
              drill table from cell
              set usage_date from x
              set app_name from y
              end click
              diagram heatmap
              x = a y
              value = c
              end heatmap
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end section
            """);

        var effect = doc.Cards[0].ClickBehaviour!.Value.Effects[0];
        Assert.Equal("DrillTableFromCell", CardClickEffectTestData.CaseName(effect));
        Assert.Equal(2, System.Linq.Enumerable.Count(System.Linq.Enumerable.Cast<object>((System.Collections.IEnumerable)CardClickEffectTestData.Fields(effect)[0]!)));
    }

    [Fact]
    public void Parse_on_click_rejects_unknown_show_format()
    {
        var ex = Assert.Throws<DashSpecParseException>(() => DashSpecTestRowTypes.ParseDashboard("""
            module section demo
              report
              title = "demo"
              card c as "C"
              on click
              show below as chart from tooltip
              end click
              diagram heatmap
              x = a y
              value = c
              end heatmap
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end section
            """));

        Assert.Contains("list, plain, or kv", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_on_click_show_accepts_split_on_tooltip_source()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module section demo
              report
              title = "demo"
              card c as "C"
              on click
              show below as list from tooltip split "; "
              end click
              tooltip peak_apps
              source = peak_apps
              end tooltip
              diagram heatmap
              x = a y
              value = c
              end heatmap
              inspect
              use tooltip peak_apps
              end inspect
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end section
            """);

        var effect = doc.Cards[0].ClickBehaviour!.Value.Effects[0];
        Assert.Equal("ShowSelection", CardClickEffectTestData.CaseName(effect));
        Assert.Equal("; ", ((Microsoft.FSharp.Core.FSharpOption<string>)CardClickEffectTestData.Fields(effect)[4]!)?.Value);
    }

    [Fact]
    public void CardDrillSlotTargets_resolves_sourced_drill_slots()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module section t
              report
              title = "T"
              filter app_name
                bind field
                  column = app_name
                end bind
                show
                  label = "App"
                end show
              end filter
              card peak as "Peak"
              on click
              | source        | drill     | set        |
              | cell          | drill     | app_name=y |
              | category item | app_drill | app_name=y |
              end click
              diagram heatmap
              x = a y
              value = c
              end heatmap
              diagram ref drill cell_table
              diagram ref app_drill app_table
              data flow
              fixture_src [rows] -> [rows] __diagram__
              fixture_src [rows] -> [rows] drill
              fixture_src [rows] -> [rows] app_drill
              end data flow
              end card
              end report
            end section
            """);

        var targets = CardDrillSlotTargets.Resolve(doc.Cards[0]);
        Assert.False(targets.LegacyAllSlots);
        Assert.True(targets.IsTarget("drill"));
        Assert.True(targets.IsTarget("app_drill"));
        Assert.False(targets.IsTarget("__diagram__"));
        Assert.Equal("cell", targets.SourceOf("drill"));
        Assert.Equal("category item", targets.SourceOf("app_drill"));
        Assert.Null(targets.SourceOf("__diagram__"));
    }

    [Fact]
    public void CardDrillSlotTargets_switch_slot_collects_sources_from_dotted_targets()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module section t
              report
              title = "T"
              filter app_name
                bind field
                  column = app_name
                end bind
                show
                  label = "App"
                end show
              end filter
              card peak as "Peak"
              on click
              | source        | drill             | set        |
              | cell          | drill.peak_table  | app_name=y |
              | category item | drill.users_table | app_name=y |
              end click
              diagram heatmap
              x = a y
              value = c
              end heatmap
              diagram ref drill
                payload peak_table cell_table
                payload users_table app_table
              data flow
              fixture_src [rows] -> [rows] __diagram__
              fixture_src [rows] -> [rows] drill.peak_table
              fixture_src [rows] -> [rows] drill.users_table
              end data flow
              end card
              end report
            end section
            """);

        var targets = CardDrillSlotTargets.Resolve(doc.Cards[0]);
        Assert.False(targets.LegacyAllSlots);
        Assert.True(targets.IsTarget("drill"));
        Assert.False(targets.IsTarget("__diagram__"));
        // ADR-0113: both click sources fill the same switch slot.
        Assert.Equal(2, targets.SourcesOf("drill").Count);
        Assert.Contains("cell", targets.SourcesOf("drill"));
        Assert.Contains("category item", targets.SourcesOf("drill"));
        Assert.Equal("cell", targets.SourceOf("drill"));
    }

    [Fact]
    public void CardDrillSlotTargets_legacy_drill_targets_all_slots()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module section t
              report
              title = "T"
              defaults
                filter.usage_date.range = -7d..today
              end defaults
              filter usage_date
                bind date
                  column = usage_date
                end bind
                show
                  label = "Date"
                end show
              end filter
              filter app_name
                bind field
                  column = app_name
                end bind
                show
                  label = "App"
                end show
              end filter
              card peak as "Peak"
              on click
              drill table from cell
              set usage_date from x
              set app_name from y
              end click
              diagram heatmap
              x = a y
              value = c
              end heatmap
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end section
            """);

        var targets = CardDrillSlotTargets.Resolve(doc.Cards[0]);
        Assert.True(targets.LegacyAllSlots);
        Assert.True(targets.IsTarget("drill"));
        Assert.True(targets.IsTarget("app_drill"));
        Assert.Equal("cell", targets.SourceOf("drill"));
    }

    [Fact]
    public void Parse_on_click_row_source_targets_drill_slot()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module section t
              report
              title = "T"
              filter app_name
                bind field
                  column = app_name
                end bind
                show
                  label = "App"
                end show
              end filter
              card app_users as "Apps"
              on click
              | source | drill | set        |
              | row    | users | app_name=y |
              end click
              diagram table
              columns = app_name
              end table
              diagram ref users user_table
              data flow
              fixture_src [rows] -> [rows] __diagram__
              fixture_src [rows] -> [rows] users
              end data flow
              end card
              end report
            end section
            """);

        var card = doc.Cards[0];
        var handler = Assert.Single(card.ClickHandlers);
        Assert.Equal("row", handler.Source);
        Assert.Equal("users", handler.Effects[0].TryGetDrillTarget?.Value);
        var targets = CardDrillSlotTargets.Resolve(card);
        Assert.True(targets.IsTarget("users"));
        Assert.Equal("row", targets.SourceOf("users"));
    }

    [Fact]
    public void Parse_on_click_unknown_bind_filter_fails_validation_loudly()
    {
        var ex = Assert.Throws<DashSpecParseException>(() => DashSpecTestRowTypes.ParseDashboard("""
            module section t
              report
              title = "T"
              card c as "C"
              on click
              | source | drill | set     |
              | row    | users | ghost=y |
              end click
              diagram table
              columns = a
              end table
              diagram ref users user_table
              data flow
              fixture_src [rows] -> [rows] __diagram__
              fixture_src [rows] -> [rows] users
              end data flow
              end card
              end report
            end section
            """));

        Assert.Contains("ghost", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unknown filter", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
