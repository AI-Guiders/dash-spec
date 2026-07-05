using DashSpec.Abstractions.Plugins;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class PhraseTemplateTests
{
    [Fact]
    public void Parse_on_click_invoke_with_call_args()
    {
        var doc = DashSpecParser.Parse("""
            @tab t {
              report "T" {
                card c as "C" {
                  on click {
                    invoke drill_down(from = y, target = user_name)
                  }
                  diagram heatmap { x = a y = b value = c }
                  datasource view dbo.t
                }
              }
            }
            """);

        var invoke = Assert.IsType<InvokeHandlerEffect>(doc.Cards[0].ClickBehaviour!.Effects[0]);
        Assert.Equal("drill_down", invoke.HandlerId);
        Assert.Equal("y", invoke.Args["from"]);
        Assert.Equal("user_name", invoke.Args["target"]);
    }

    [Fact]
    public void Parse_on_click_phrase_template()
    {
        var options = new DashSpecParseOptions
        {
            PhraseTemplates =
            [
                new PhraseTemplateDescriptor(
                    "card_export",
                    "csv_export",
                    PhraseScopes.OnClick,
                    "export card as {format} with delimiter {delimiter}",
                    [
                        new PhraseSlotDescriptor("format", PhraseSlotKind.Ident),
                        new PhraseSlotDescriptor("delimiter", PhraseSlotKind.String),
                    ]),
            ],
        };

        var doc = DashSpecParser.Parse("""
            @tab t {
              report "T" {
                card c as "C" {
                  on click {
                    export card as csv with delimiter ";"
                  }
                  diagram heatmap { x = a y = b value = c }
                  datasource view dbo.t
                }
              }
            }
            """, specDirectory: null, options);

        var invoke = Assert.IsType<InvokeHandlerEffect>(doc.Cards[0].ClickBehaviour!.Effects[0]);
        Assert.Equal("csv_export", invoke.HandlerId);
        Assert.Equal("csv", invoke.Args["format"]);
        Assert.Equal(";", invoke.Args["delimiter"]);
    }
}
