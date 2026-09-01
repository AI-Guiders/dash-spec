using System.Text.Json;
using System.Text.Json.Serialization;
using DashSpec.Host.Commands;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace DashSpec.Host.Tests.Conformance;

public sealed class InputCommandLineAcceptKeysConformanceTests
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void DashboardFilterCommandKeys_matches_input_command_line_accept_keys_vectors()
    {
        var specPath = Path.Combine(AppContext.BaseDirectory, "Conformance", "input-command-line-accept-keys.spec.json");
        var json = File.ReadAllText(specPath);
        var spec = JsonSerializer.Deserialize<InputCommandLineAcceptKeysSpecDocument>(json, JsonOptions)
                   ?? throw new InvalidOperationException("input-command-line-accept-keys spec deserialized to null.");

        var errors = new List<string>();
        foreach (var vector in spec.Vectors)
        {
            var args = new KeyboardEventArgs
            {
                Key = vector.Key,
                CtrlKey = vector.CtrlKey,
                AltKey = vector.AltKey,
                MetaKey = vector.MetaKey,
                ShiftKey = vector.ShiftKey,
            };

            var accept = DashboardFilterCommandKeys.IsAcceptCompletion(args);
            var prevent = DashboardFilterCommandKeys.PreventDefaultWhenSuggestOpen(args, suggestOpen: true);

            if (accept != vector.Expect.IsAcceptCompletion)
            {
                errors.Add($"[{vector.Id}] isAcceptCompletion expected {vector.Expect.IsAcceptCompletion}, got {accept}.");
            }

            if (prevent != vector.Expect.PreventDefaultWhenSuggestOpen)
            {
                errors.Add($"[{vector.Id}] preventDefaultWhenSuggestOpen expected {vector.Expect.PreventDefaultWhenSuggestOpen}, got {prevent}.");
            }
        }

        Assert.Empty(errors);
    }

    sealed record InputCommandLineAcceptKeysSpecDocument(
        [property: JsonPropertyName("vectors")] IReadOnlyList<InputCommandLineAcceptKeysVector> Vectors);

    sealed record InputCommandLineAcceptKeysVector(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("ctrlKey")] bool CtrlKey,
        [property: JsonPropertyName("altKey")] bool AltKey,
        [property: JsonPropertyName("metaKey")] bool MetaKey,
        [property: JsonPropertyName("shiftKey")] bool ShiftKey,
        [property: JsonPropertyName("expect")] InputCommandLineAcceptKeysExpectation Expect);

    sealed record InputCommandLineAcceptKeysExpectation(
        [property: JsonPropertyName("isAcceptCompletion")] bool IsAcceptCompletion,
        [property: JsonPropertyName("preventDefaultWhenSuggestOpen")] bool PreventDefaultWhenSuggestOpen);
}
