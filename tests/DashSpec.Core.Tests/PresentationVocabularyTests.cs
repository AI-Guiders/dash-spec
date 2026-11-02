using DashSpec.Execution.Parsing;
using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

/// <summary>Plugin-declared presentation vocabulary (one knob, one owner): an unknown
/// property is a loud parse error until a plugin declares it via
/// registerPresentationProperties — the bridge the kernel feeds from active plugins.</summary>
public class PresentationVocabularyTests
{
    private const string CardWithKnob = """
        module dashboard vocab_probe
        namespace Vocab.Probe

        report
        title = "T"
        card a as "A"
          presentation
            lane_height = 42
          end presentation
          diagram number
          value = x
          end number
          data flow { fixture_src [rows] -> [rows] __diagram__ }
        end card
        end report
        end dashboard
        """;

    [Fact]
    public void Unknown_presentation_property_is_a_loud_parse_error()
    {
        var ex = Assert.Throws<DashSpecParseException>(
            () => DashSpecParser.Parse(CardWithKnob, DashSpecTestRowTypes.FixtureDir));
        Assert.Contains("Unknown property 'lane_height'", ex.Message);
    }

    [Fact]
    public void Declared_plugin_property_parses()
    {
        DashSpec.Modeling.Parse.PropertySchemas.registerPresentationProperties(["lane_height"]);
        var document = DashSpecParser.Parse(CardWithKnob, DashSpecTestRowTypes.FixtureDir);
        Assert.Equal("vocab_probe", document.Id);
        // Registry is process-wide by design (kernels only extend); the unique knob
        // name keeps this declaration from loosening other tests.
    }
}
