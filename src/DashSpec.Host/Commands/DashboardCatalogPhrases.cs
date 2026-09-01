#nullable enable

using AIGuiders.Platform.Authoring.Command.Catalog;

namespace DashSpec.Host.Commands;

/// <summary>Phrase materialization from <c>dash.catalog</c> SSOT (console flavor wire).</summary>
internal static class DashboardCatalogPhrases
{
    public const string ShowHostPhrase = "show-host";

    public static string Materialize(string phraseName, IReadOnlyDictionary<string, string> slots)
    {
        var template = ResolvePhrase(phraseName);
        var result = template;
        foreach (var (slot, value) in slots)
        {
            result = result.Replace($"{{{slot}}}", value, StringComparison.Ordinal);
        }

        return result;
    }

    public static string ResolvePhrase(string phraseName) =>
        DashboardCatalog.Current.Phrases
            .FirstOrDefault(phrase => string.Equals(phrase.Name, phraseName, StringComparison.OrdinalIgnoreCase))
            ?.Phrase
        ?? throw new InvalidOperationException($"dash.catalog missing phrase '{phraseName}'.");

    public static bool TryReadSlot(
        string phraseName,
        string canonicalPath,
        string slotName,
        out string value)
    {
        value = "";
        var template = ResolvePhrase(phraseName);
        var slotToken = $"{{{slotName}}}";
        var slotIndex = template.IndexOf(slotToken, StringComparison.Ordinal);
        if (slotIndex < 0)
        {
            return false;
        }

        var prefix = template[..slotIndex];
        var suffix = template[(slotIndex + slotToken.Length)..];
        if (!canonicalPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !canonicalPath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value = canonicalPath[prefix.Length..^(suffix.Length)];
        return value.Length > 0;
    }
}
