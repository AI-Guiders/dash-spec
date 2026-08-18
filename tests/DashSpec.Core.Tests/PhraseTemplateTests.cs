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
            @tab t
              report
              title = "T"
              card c as "C"
              on click
              invoke drill_down(from = y, target = user_name)
              end click
              diagram heatmap
              x = a y
              value = c
              end heatmap
              datasource view dbo.t
              end card
              end report
            end tab
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
            KnownActionHandlers = new HashSet<string>(["csv_export"], StringComparer.OrdinalIgnoreCase),
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
            @tab t
              report
              title = "T"
              card c as "C"
              on click
              invoke csv_export(format = csv, delimiter = ";")
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
        Assert.Equal("csv_export", invoke.HandlerId);
        Assert.Equal("csv", invoke.Args["format"]);
        Assert.Equal(";", invoke.Args["delimiter"]);
    }
}
