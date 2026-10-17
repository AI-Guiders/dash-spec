using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime;
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

    [Fact]
    public void DescribeOversize_axis_limit_is_distinct_from_cell_product()
    {
        var limits = MatrixRenderLimitsDefinition.Default;
        Assert.True(limits.IsOversized(120, 16));
        Assert.Contains("столбцов X (120)", MatrixRenderLimits.DescribeOversize(limits, 120, 16));
        Assert.DoesNotContain("ячеек", MatrixRenderLimits.DescribeOversize(limits, 120, 16));
    }
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

        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module tab t
              connect
              use flow fixture_flow
              end connect
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
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end tab
            """, specDirectory: null, options);

        var effect = doc.Cards[0].ClickBehaviour!.Value.Effects[0];
        var fields = CardClickEffectTestData.Fields(effect);
        Assert.Equal("InvokeHandler", CardClickEffectTestData.CaseName(effect));
        Assert.Equal("drill_down", fields[0]!);
        var args = (IReadOnlyDictionary<string, string>)fields[1]!;
        Assert.Equal("detail", args["tab"]);
        Assert.Equal("user_name", args["target"]);
        Assert.Equal("y", args["from"]);
    }
}
