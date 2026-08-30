#nullable enable
using AIGuiders.Platform.CommandPlane;

namespace DashSpec.Host.Commands;

/// <summary>CLI-facing labels for dash-ccl (no leading slashes).</summary>
internal static class DashboardFilterCommandDisplay
{
    public const string RootVerb = "select";
    public const string Prompt = ">";
    public const string EmptyPlaceholder = "select date · select user …";

    public static SlashInputGuidance ForCli(SlashInputGuidance guidance) =>
        guidance with
        {
            Breadcrumb = FormatBreadcrumb(guidance.Breadcrumb),
            Placeholder = FormatPlaceholder(guidance.Placeholder),
        };

    public static string FormatCommand(string text)
    {
        var line = text.Trim();
        if (line.StartsWith('/'))
        {
            line = line[1..].TrimStart();
        }

        return line;
    }

    public static string FormatSuggestion(SlashCompletionItem item) =>
        !string.IsNullOrWhiteSpace(item.StepSegment)
            ? $"{RootVerb} {item.StepSegment}"
            : FormatCommand(item.InsertText.TrimEnd());

    static string FormatBreadcrumb(string breadcrumb)
    {
        if (string.IsNullOrWhiteSpace(breadcrumb))
        {
            return RootVerb;
        }

        var text = breadcrumb.TrimStart('/').Replace(" › ", " ");
        if (!text.StartsWith(RootVerb, StringComparison.OrdinalIgnoreCase))
        {
            text = $"{RootVerb} {text}";
        }

        return text;
    }

    static string FormatPlaceholder(string placeholder) =>
        placeholder switch
        {
            "Type a command path" => EmptyPlaceholder,
            "Continue typing the command path" => EmptyPlaceholder,
            _ when placeholder.StartsWith("Next: ", StringComparison.Ordinal) =>
                $"{RootVerb} {placeholder["Next: ".Length..]}",
            _ => FormatCommand(placeholder),
        };
}
