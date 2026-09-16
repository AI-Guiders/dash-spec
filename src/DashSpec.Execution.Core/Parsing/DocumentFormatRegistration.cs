using DashSpec.Core.Authoring;
using DashSpec.Core.Authoring.EditorConfig;
using DashSpec.Modeling.Parse.Formatting;

namespace DashSpec.Execution.Parsing;

/// <summary>Wire F# block formatter into Core pipeline (DASHSPEC-ADR-0053).</summary>
internal static class DocumentFormatRegistration
{
    internal static void Register()
    {
        DocumentFormatBridge.FormatDashSpec = (text, options) =>
            DashSpecBlockFormatter.format(text, ToFsharp(options));
    }

    static DashSpecFormatOptions ToFsharp(EditorConfigOptions options) =>
        new()
        {
            IndentStyle = options.IndentStyle,
            IndentSize = options.IndentSize,
            EndOfLine = options.EndOfLine,
            TrimTrailingWhitespace = options.TrimTrailingWhitespace,
            InsertFinalNewline = options.InsertFinalNewline,
            DashSpecFormatOnSave = options.DashSpecFormatOnSave,
            DashSpecMaxConsecutiveBlankLines = options.DashSpecMaxConsecutiveBlankLines,
            DashSpecIndentBlockBody = options.DashSpecIndentBlockBody,
            DashSpecPreserveBlankLineBeforeEnd = options.DashSpecPreserveBlankLineBeforeEnd,
            DashSpecBlankLineBetweenBlocks = options.DashSpecBlankLineBetweenBlocks,
        };
}
