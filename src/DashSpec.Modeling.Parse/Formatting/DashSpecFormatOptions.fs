namespace DashSpec.Modeling.Parse.Formatting

/// <summary>EditorConfig + DashSpec formatter options (DASHSPEC-ADR-0053).</summary>
[<CLIMutable>]
type DashSpecFormatOptions =
    { IndentStyle: string
      IndentSize: int
      EndOfLine: string
      TrimTrailingWhitespace: bool
      InsertFinalNewline: bool
      DashSpecFormatOnSave: bool
      DashSpecMaxConsecutiveBlankLines: int
      DashSpecIndentBlockBody: bool
      DashSpecPreserveBlankLineBeforeEnd: bool
      DashSpecBlankLineBetweenBlocks: bool }

module DashSpecFormatOptions =
    let defaultOptions =
        { IndentStyle = "space"
          IndentSize = 4
          EndOfLine = "lf"
          TrimTrailingWhitespace = true
          InsertFinalNewline = true
          DashSpecFormatOnSave = true
          DashSpecMaxConsecutiveBlankLines = 1
          DashSpecIndentBlockBody = true
          DashSpecPreserveBlankLineBeforeEnd = true
          DashSpecBlankLineBetweenBlocks = true }

    let indentUnit (options: DashSpecFormatOptions) =
        if options.IndentStyle.Equals("tab", System.StringComparison.OrdinalIgnoreCase) then "\t"
        else System.String(' ', max 1 options.IndentSize)

    let newLine (options: DashSpecFormatOptions) =
        match options.EndOfLine.ToLowerInvariant() with
        | "crlf" -> "\r\n"
        | "cr" -> "\r"
        | _ -> "\n"
