namespace DashSpec.Modeling.Parse.Formatting

open System
open System.Collections.Generic

module DashSpecBlockFormatter =

    type private BlockFrame = { EndIndent: int; ContentIndent: int }

    let private normalizeNewlines (text: string) =
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')

    let private pad (indentUnits: int) (unit: string) (content: string) =
        if indentUnits <= 0 then content
        else String.Concat(Array.create indentUnits unit) + content

    let private resolveContentIndent (stack: Stack<BlockFrame>) (moduleStarted: bool) =
        if stack.Count > 0 then stack.Peek().ContentIndent
        elif moduleStarted then 1
        else 0

    let format (text: string) (options: DashSpecFormatOptions) =
        if not options.DashSpecIndentBlockBody then normalizeNewlines text
        else
            let unit = DashSpecFormatOptions.indentUnit options
            let rawLines = (normalizeNewlines text).Split('\n')
            let output = ResizeArray<string>(rawLines.Length)
            let stack = Stack<BlockFrame>()
            let mutable moduleStarted = false
            let mutable previousNonBlank = ""

            for rawLine in rawLines do
                if rawLine.Length = 0 then output.Add("")
                else
                    let trimmed = rawLine.Trim()
                    if trimmed.Length = 0 then output.Add("")
                    else
                        match BlockFormatterRules.classifyLine trimmed with
                        | BlockFormatterRules.Blank -> output.Add("")
                        | BlockFormatterRules.End _ ->
                            if
                                options.DashSpecPreserveBlankLineBeforeEnd
                                && output.Count > 0
                                && output.[output.Count - 1].Length > 0
                                && not (String.IsNullOrWhiteSpace previousNonBlank)
                                && not (previousNonBlank.StartsWith("end ", StringComparison.OrdinalIgnoreCase))
                            then
                                output.Add("")

                            let endIndent =
                                if stack.Count > 0 then stack.Pop().EndIndent
                                elif moduleStarted then 1
                                else 0

                            output.Add(pad endIndent unit trimmed)
                            previousNonBlank <- trimmed
                        | BlockFormatterRules.BraceClose ->
                            let closeIndent =
                                if stack.Count > 0 then stack.Pop().EndIndent
                                elif moduleStarted then 1
                                else 0

                            output.Add(pad closeIndent unit trimmed)
                            previousNonBlank <- trimmed
                        | BlockFormatterRules.ModuleHeader _ ->
                            let indentUnits = resolveContentIndent stack moduleStarted
                            output.Add(pad indentUnits unit trimmed)
                            moduleStarted <- true
                            stack.Push({ EndIndent = 1; ContentIndent = 1 })
                            previousNonBlank <- trimmed
                        | BlockFormatterRules.BlockOpener _ ->
                            let indentUnits = resolveContentIndent stack moduleStarted
                            output.Add(pad indentUnits unit trimmed)
                            stack.Push({ EndIndent = indentUnits; ContentIndent = indentUnits + 1 })
                            previousNonBlank <- trimmed
                        | BlockFormatterRules.BraceOpen ->
                            let indentUnits = resolveContentIndent stack moduleStarted
                            output.Add(pad indentUnits unit trimmed)
                            stack.Push({ EndIndent = indentUnits; ContentIndent = indentUnits + 1 })
                            previousNonBlank <- trimmed
                        | BlockFormatterRules.Content ->
                            let indentUnits = resolveContentIndent stack moduleStarted
                            output.Add(pad indentUnits unit trimmed)
                            previousNonBlank <- trimmed

            String.Join('\n', output)
