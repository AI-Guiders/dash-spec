using DashSpec.Core.Parsing;
using DashSpec.Abstractions.Plugins;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class CardVisibilityAndPhaseTests
{
    private static DashSpecParseOptions ClickOptions => new()
    {
        PhraseTemplates =
        [
            new PhraseTemplateDescriptor(
                "on_click_default",
                "drill_down",
                PhraseScopes.OnClick,
                "drill to {tab} with {target} from {from}",
                [
                    new("tab", PhraseSlotKind.Ident),
                    new("target", PhraseSlotKind.Ident),
                    new("from", PhraseSlotKind.Ident),
                ]),
        ],
        KnownInteractionHandlers = new HashSet<string>(["drill_down"], StringComparer.OrdinalIgnoreCase),
    };

    [Fact]
    public void Parse_limits_and_when_oversize_on_matrix_card()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module tab t
              report
              title = "T"
              card heat as "H"
              limits
              axis = 120
              cells = 3000
              end limits
              when oversize
              message = "Too dense — narrow filters."
              end when
              diagram heatmap
              x = a y
              value = c
              end heatmap
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end tab
            """);

        var card = doc.Cards[0];
        Assert.Equal(120, card.MatrixLimits!.Value.MaxAxisLabels?.Value);
        Assert.Equal(3000, card.MatrixLimits.Value.MaxCells?.Value);
        Assert.Equal("Too dense — narrow filters.", card.OversizeMessage);
        Assert.False(card.MatrixLimits.Value.IsOversized(90, 1));
        Assert.True(card.MatrixLimits.Value.IsOversized(121, 1));
    }

    [Fact]
    public void Parse_when_empty_and_message_block()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module tab t
              report
              title = "T"
              card browse as "Browse"
              when user_name empty
              diagram bar
              x = a y = b
              end bar
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              card detail as "Detail"
              when user_name
              message = "Pick a user"
              end user_name
              diagram heatmap
              x = a y
              value = c
              end heatmap
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end tab
            """);

        Assert.Equal(CardVisibilityMode.WhenEmpty, doc.Cards[0].Visibility!.Value.Mode);
        Assert.Equal(CardVisibilityMode.WhenSet, doc.Cards[1].Visibility!.Value.Mode);
        Assert.Equal("Pick a user", doc.Cards[1].Visibility!.Value.Message?.Value);
    }

    [Fact]
    public void Parse_phase_and_focus()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module tab t
              report
              title = "T"
              phase browse
              card browse as "B"
              on click
              focus detail
              end click
              diagram bar
              x = a y = b
              end bar
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end phase
              phase detail
              card heat as "H"
              diagram heatmap
              x = a y
              value = c
              end heatmap
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end phase
              end report
            end tab
            """);

        Assert.Equal("browse", doc.Cards[0].PhaseId);
        Assert.Equal("detail", doc.Cards[1].PhaseId);
        Assert.Equal("FocusPhase", CardClickEffectTestData.CaseName(doc.Cards[0].ClickBehaviour!.Value.Effects[0]));
    }

    [Fact]
    public void Parse_browse_bar_click_set_and_focus()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module tab t
              report
              title = "T"
              phase browse
              card browse as "B" ref Eb
              when user_name empty
              on click
              set user_name from y
              focus detail
              end click
              diagram bar
              x = a y = b
              end bar
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end phase
              end report
            end tab
            """);

        var effects = doc.Cards[0].ClickBehaviour!.Value.Effects;
        Assert.Equal("SetFilterFromField", CardClickEffectTestData.CaseName(effects[0]));
        Assert.Equal("FocusPhase", CardClickEffectTestData.CaseName(effects[1]));
    }
}
