using System.Text.RegularExpressions;
using DashSpec.Abstractions.Plugins;

namespace DashSpec.Core.Parsing;

internal static partial class PhraseTemplateMatcher
{
    public static bool TryMatch(
        IReadOnlyList<PhraseToken> tokens,
        PhraseTemplateDescriptor template,
        out string handlerId,
        out Dictionary<string, string> args)
    {
        handlerId = template.HandlerId;
        args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var slotIndex = template.Slots.ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
        var parts = CompilePattern(template.Pattern, slotIndex);
        var index = 0;

        foreach (var part in parts)
        {
            if (part.IsSlot)
            {
                if (index >= tokens.Count)
                {
                    if (part.Optional)
                    {
                        continue;
                    }

                    return false;
                }

                var token = tokens[index];
                if (!TryCoerceToken(token, part.SlotKind, out var value))
                {
                    return false;
                }

                args[part.SlotName!] = value;
                index++;
                continue;
            }

            if (index >= tokens.Count ||
                !string.Equals(tokens[index].Value, part.Literal, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            index++;
        }

        return index == tokens.Count;
    }

    public static bool TryMatchAny(
        IReadOnlyList<PhraseToken> tokens,
        IEnumerable<PhraseTemplateDescriptor> templates,
        out PhraseTemplateDescriptor matched,
        out Dictionary<string, string> args)
    {
        foreach (var template in templates)
        {
            if (TryMatch(tokens, template, out _, out args))
            {
                matched = template;
                return true;
            }
        }

        matched = null!;
        args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return false;
    }

    private static bool TryCoerceToken(PhraseToken token, PhraseSlotKind kind, out string value)
    {
        value = token.Value;
        return kind switch
        {
            PhraseSlotKind.String => token.Kind is PhraseTokenKind.String or PhraseTokenKind.Ident,
            PhraseSlotKind.Int => token.Kind is PhraseTokenKind.Int ||
                                  (token.Kind is PhraseTokenKind.Ident && int.TryParse(token.Value, out _)),
            _ => token.Kind is PhraseTokenKind.Ident or PhraseTokenKind.String,
        };
    }

    private static List<PatternPart> CompilePattern(
        string pattern,
        IReadOnlyDictionary<string, PhraseSlotDescriptor> slots)
    {
        var parts = new List<PatternPart>();
        var index = 0;

        while (index < pattern.Length)
        {
            var slotStart = pattern.IndexOf('{', index);
            if (slotStart < 0)
            {
                AddLiterals(parts, pattern[index..]);
                break;
            }

            AddLiterals(parts, pattern[index..slotStart]);
            var slotEnd = pattern.IndexOf('}', slotStart + 1);
            if (slotEnd < 0)
            {
                throw new DashSpecParseException($"Invalid phrase template pattern: unclosed '{{' in '{pattern}'.");
            }

            var slotName = pattern[(slotStart + 1)..slotEnd];
            var optional = false;
            if (slotName.EndsWith('?'))
            {
                optional = true;
                slotName = slotName[..^1];
            }

            if (!slots.TryGetValue(slotName, out var slot))
            {
                slot = new PhraseSlotDescriptor(slotName, PhraseSlotKind.Ident, optional);
            }

            parts.Add(new PatternPart(true, null, slotName, slot.Kind, optional || slot.Optional));
            index = slotEnd + 1;
        }

        return parts;
    }

    private static void AddLiterals(List<PatternPart> parts, string literalSegment)
    {
        foreach (var literal in literalSegment.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            parts.Add(new PatternPart(false, literal, null, PhraseSlotKind.Ident, false));
        }
    }

    private readonly record struct PatternPart(
        bool IsSlot,
        string? Literal,
        string? SlotName,
        PhraseSlotKind SlotKind,
        bool Optional);
}

internal readonly record struct PhraseToken(PhraseTokenKind Kind, string Value);

internal enum PhraseTokenKind
{
    Ident,
    String,
    Int,
}
