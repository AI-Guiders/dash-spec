using System.Text.Json;
using System.Text.Json.Serialization;
using DashSpec.Host.Commands;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace DashSpec.Host.Tests.Conformance;

public sealed class CclAcceptKeysConformanceTests
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void DashboardFilterCommandKeys_matches_ccl_accept_keys_vectors()
    {
        var specPath = Path.Combine(AppContext.BaseDirectory, "Conformance", "ccl-accept-keys.spec.json");
        var json = File.ReadAllText(specPath);
        var spec = JsonSerializer.Deserialize<CclAcceptKeysSpecDocument>(json, JsonOptions)
                   ?? throw new InvalidOperationException("ccl-accept-keys spec deserialized to null.");

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

    sealed record CclAcceptKeysSpecDocument(
        [property: JsonPropertyName("vectors")] IReadOnlyList<CclAcceptKeysVector> Vectors);

    sealed record CclAcceptKeysVector(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("ctrlKey")] bool CtrlKey,
        [property: JsonPropertyName("altKey")] bool AltKey,
        [property: JsonPropertyName("metaKey")] bool MetaKey,
        [property: JsonPropertyName("shiftKey")] bool ShiftKey,
        [property: JsonPropertyName("expect")] CclAcceptKeysExpectation Expect);

    sealed record CclAcceptKeysExpectation(
        [property: JsonPropertyName("isAcceptCompletion")] bool IsAcceptCompletion,
        [property: JsonPropertyName("preventDefaultWhenSuggestOpen")] bool PreventDefaultWhenSuggestOpen);
}
