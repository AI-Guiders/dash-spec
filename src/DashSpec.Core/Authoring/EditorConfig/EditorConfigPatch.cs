namespace DashSpec.Core.Authoring.EditorConfig;

internal sealed class EditorConfigPatch
{
    public bool HasIndentStyle { get; private set; }
    public string IndentStyle { get; private set; } = "space";
    public bool HasIndentSize { get; private set; }
    public int IndentSize { get; private set; }
    public bool HasTabWidth { get; private set; }
    public int TabWidth { get; private set; }
    public bool HasEndOfLine { get; private set; }
    public string EndOfLine { get; private set; } = "lf";
    public bool HasTrimTrailingWhitespace { get; private set; }
    public bool TrimTrailingWhitespace { get; private set; }
    public bool HasInsertFinalNewline { get; private set; }
    public bool InsertFinalNewline { get; private set; }
    public bool HasDashSpecFormatOnSave { get; private set; }
    public bool DashSpecFormatOnSave { get; private set; }
    public bool HasDashSpecMaxConsecutiveBlankLines { get; private set; }
    public int DashSpecMaxConsecutiveBlankLines { get; private set; }
    public bool HasDashSpecIndentBlockBody { get; private set; }
    public bool DashSpecIndentBlockBody { get; private set; }
    public bool HasDashSpecPreserveBlankLineBeforeEnd { get; private set; }
    public bool DashSpecPreserveBlankLineBeforeEnd { get; private set; }
    public bool HasDashSpecBlankLineBetweenBlocks { get; private set; }
    public bool DashSpecBlankLineBetweenBlocks { get; private set; }

    public void Set(string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case "indent_style":
                HasIndentStyle = true;
                IndentStyle = value;
                break;
            case "indent_size" when int.TryParse(value, out var indentSize):
                HasIndentSize = true;
                IndentSize = indentSize;
                break;
            case "tab_width" when int.TryParse(value, out var tabWidth):
                HasTabWidth = true;
                TabWidth = tabWidth;
                break;
            case "end_of_line":
                HasEndOfLine = true;
                EndOfLine = value;
                break;
            case "trim_trailing_whitespace":
                HasTrimTrailingWhitespace = true;
                TrimTrailingWhitespace = ParseBool(value);
                break;
            case "insert_final_newline":
                HasInsertFinalNewline = true;
                InsertFinalNewline = ParseBool(value);
                break;
            case "dashspec_format_on_save":
                HasDashSpecFormatOnSave = true;
                DashSpecFormatOnSave = ParseBool(value);
                break;
            case "dashspec_max_consecutive_blank_lines" when int.TryParse(value, out var maxBlank):
                HasDashSpecMaxConsecutiveBlankLines = true;
                DashSpecMaxConsecutiveBlankLines = Math.Max(0, maxBlank);
                break;
            case "dashspec_indent_block_body":
                HasDashSpecIndentBlockBody = true;
                DashSpecIndentBlockBody = ParseBool(value);
                break;
            case "dashspec_preserve_blank_line_before_end":
                HasDashSpecPreserveBlankLineBeforeEnd = true;
                DashSpecPreserveBlankLineBeforeEnd = ParseBool(value);
                break;
            case "dashspec_blank_line_between_blocks":
                HasDashSpecBlankLineBetweenBlocks = true;
                DashSpecBlankLineBetweenBlocks = ParseBool(value);
                break;
        }
    }

    static bool ParseBool(string value) =>
        bool.TryParse(value, out var parsed) && parsed;
}
