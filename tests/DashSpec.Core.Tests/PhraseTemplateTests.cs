using DashSpec.Abstractions.Plugins;
using Xunit;

using DashSpec.Execution.Runtime;

using DashSpec.Compiler.Contract;

namespace DashSpec.Core.Tests;

public sealed class PhraseTemplateTests
{
    [Fact]
    public void Parse_on_click_invoke_with_call_args()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module tab t
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
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end tab
            """);

        var effect = doc.Cards[0].ClickBehaviour!.Value.Effects[0];
        var fields = CardClickEffectTestData.Fields(effect);
        Assert.Equal("InvokeHandler", CardClickEffectTestData.CaseName(effect));
        Assert.Equal("drill_down", fields[0]!);
        var args = (IReadOnlyDictionary<string, string>)fields[1]!;
        Assert.Equal("y", args["from"]);
        Assert.Equal("user_name", args["target"]);
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

        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module tab t
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
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end tab
            """, specDirectory: null, options);

        var effect = doc.Cards[0].ClickBehaviour!.Value.Effects[0];
        var fields = CardClickEffectTestData.Fields(effect);
        Assert.Equal("InvokeHandler", CardClickEffectTestData.CaseName(effect));
        Assert.Equal("csv_export", fields[0]!);
        var args = (IReadOnlyDictionary<string, string>)fields[1]!;
        Assert.Equal("csv", args["format"]);
        Assert.Equal(";", args["delimiter"]);
    }
}
