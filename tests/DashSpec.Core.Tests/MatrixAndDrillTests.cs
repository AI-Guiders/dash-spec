using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class MatrixRenderLimitsTests
{
    [Theory]
    [InlineData(50, 50, false)]
    [InlineData(80, 30, false)]
    [InlineData(100, 30, true)]
    [InlineData(100, 100, true)]
    [InlineData(90, 90, true)]
    public void IsOversized(int x, int y, bool expected) =>
        Assert.Equal(expected, MatrixRenderLimits.IsOversized(x, y));
}

public sealed class DrillDownPhraseTests
{
    [Fact]
    public void Parse_phrase_drill_to_tab()
    {
        var options = new DashSpecParseOptions
        {
            PhraseTemplates =
            [
                new DashSpec.Abstractions.Plugins.PhraseTemplateDescriptor(
                    "on_click_default",
                    "drill_down",
                    DashSpec.Abstractions.Plugins.PhraseScopes.OnClick,
                    "drill to {tab} with {target} from {from}",
                    [
                        new("tab", DashSpec.Abstractions.Plugins.PhraseSlotKind.Ident),
                        new("target", DashSpec.Abstractions.Plugins.PhraseSlotKind.Ident),
                        new("from", DashSpec.Abstractions.Plugins.PhraseSlotKind.Ident),
                    ]),
            ],
        };

        var doc = DashSpecParser.Parse("""
            @tab t
              report
              title = "T"
              card c as "C"
              on click
              invoke drill_down(tab = detail, target = user_name, from = y)
              end click
              diagram heatmap
              x = a y
              value = c
              end heatmap
              datasource view dbo.t
              end card
              end report
            end tab
            """, specDirectory: null, options);

        var invoke = Assert.IsType<InvokeHandlerEffect>(doc.Cards[0].ClickBehaviour!.Effects[0]);
        Assert.Equal("drill_down", invoke.HandlerId);
        Assert.Equal("detail", invoke.Args["tab"]);
        Assert.Equal("user_name", invoke.Args["target"]);
        Assert.Equal("y", invoke.Args["from"]);
    }
}
