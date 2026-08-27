using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public class CardClickBehaviourTests
{
    [Fact]
    public void Parse_on_click_show_list_from_tooltip_copy()
    {
        var doc = DashSpecParser.Parse("""
            @tab demo
              report
              title = "demo"
              standalone
              filter date usage_date on usage_date as "Date" default -7d..today
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
              datasource view dbo.t
              bind
                usage_date
              end bind
              end card
              end report
            end tab
            """);

        var card = doc.Cards[0];
        Assert.NotNull(card.ClickBehaviour);
        var show = Assert.IsType<ShowSelectionEffect>(card.ClickBehaviour!.Effects[0]);
        Assert.Equal(ShowPlacement.Below, show.Placement);
        Assert.Equal(ShowFormat.List, show.Format);
        Assert.Equal(ShowSource.Tooltip, show.Source);
        Assert.True(show.CopyFriendly);
        Assert.NotNull(card.Tooltip);
    }

    [Fact]
    public void Parse_on_click_goto_and_set_filters()
    {
        var doc = DashSpecParser.Parse("""
            @tab t
              report
              title = "T"
              standalone
              filter date usage_date on usage_date as "Date" default -7d..today
              filter field user_name on dbo.t.user as "User" widget combobox
              toolbar usage_date, user_name
              end standalone
              card peak_apps as "Peak"
              on click
              set usage_date from x
              set user_name from y
              goto tab detail
              end click
              diagram heatmap
              x = a y
              value = c
              end heatmap
              datasource view dbo.t
              bind
                usage_date, user_name
              end bind
              end card
              end report
            end tab
            """);

        var card = doc.Cards[0];
        Assert.NotNull(card.ClickBehaviour);
        Assert.Equal(3, card.ClickBehaviour!.Effects.Count);
        Assert.IsType<SetFilterFromFieldEffect>(card.ClickBehaviour.Effects[0]);
        Assert.IsType<SetFilterFromFieldEffect>(card.ClickBehaviour.Effects[1]);
        Assert.IsType<GotoTabEffect>(card.ClickBehaviour.Effects[2]);
    }

    [Fact]
    public void Parse_on_click_rejects_unknown_show_format()
    {
        var ex = Assert.Throws<DashSpecParseException>(() => DashSpecParser.Parse("""
            @tab demo
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
              datasource view dbo.t
              end card
              end report
            end tab
            """));

        Assert.Contains("list, plain, or kv", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_on_click_show_accepts_split_on_tooltip_source()
    {
        var doc = DashSpecParser.Parse("""
            @tab demo
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
              datasource view dbo.t
              end card
              end report
            end tab
            """);

        var show = Assert.IsType<ShowSelectionEffect>(doc.Cards[0].ClickBehaviour!.Effects[0]);
        Assert.Equal("; ", show.Split);
    }
}
