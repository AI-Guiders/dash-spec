namespace DashSpec.Core.Authoring.EditorConfig;

/// <summary>Resolved EditorConfig + DashSpec formatter options for a document file.</summary>
public sealed record EditorConfigOptions
{
    public static EditorConfigOptions Default { get; } = new();

    public string IndentStyle { get; init; } = "space";
    public int IndentSize { get; init; } = 4;
    public int TabWidth { get; init; } = 4;
    public string EndOfLine { get; init; } = "lf";
    public bool TrimTrailingWhitespace { get; init; } = true;
    public bool InsertFinalNewline { get; init; } = true;

    public bool DashSpecFormatOnSave { get; init; } = true;
    public int DashSpecMaxConsecutiveBlankLines { get; init; } = 1;
    public bool DashSpecIndentBlockBody { get; init; } = true;
    public bool DashSpecPreserveBlankLineBeforeEnd { get; init; } = true;

    public string IndentUnit =>
        IndentStyle.Equals("tab", StringComparison.OrdinalIgnoreCase)
            ? "\t"
            : new string(' ', Math.Max(1, IndentSize));

    public string NewLine => EndOfLine.ToLowerInvariant() switch
    {
        "crlf" => "\r\n",
        "cr" => "\r",
        _ => "\n",
    };

    
}


