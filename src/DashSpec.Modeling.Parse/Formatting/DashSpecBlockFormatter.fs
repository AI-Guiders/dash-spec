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

    let private maybeInsertBlankBetweenBlocks
        (options: DashSpecFormatOptions)
        (previousNonBlank: string)
        (previousNonBlankIndent: int)
        (currentIndent: int)
        (output: ResizeArray<string>)
        =
        if not options.DashSpecBlankLineBetweenBlocks then ()
        elif String.IsNullOrWhiteSpace previousNonBlank then ()
        elif not (previousNonBlank.StartsWith("end ", StringComparison.OrdinalIgnoreCase)) then ()
        elif previousNonBlankIndent <> currentIndent then ()
        elif output.Count = 0 then ()
        elif output.[output.Count - 1].Length = 0 then ()
        else output.Add("")

    let private maybeInsertBlankBeforeEnd
        (options: DashSpecFormatOptions)
        (previousNonBlank: string)
        (output: ResizeArray<string>)
        =
        if
            options.DashSpecPreserveBlankLineBeforeEnd
            && output.Count > 0
            && output.[output.Count - 1].Length > 0
            && not (String.IsNullOrWhiteSpace previousNonBlank)
            && not (previousNonBlank.StartsWith("end ", StringComparison.OrdinalIgnoreCase))
        then
            output.Add("")

    let private dropBlankLineBeforeEnd (output: ResizeArray<string>) =
        if output.Count > 0 && output.[output.Count - 1].Length = 0 then
            output.RemoveAt(output.Count - 1)

    let format (text: string) (options: DashSpecFormatOptions) =
        if not options.DashSpecIndentBlockBody then normalizeNewlines text
        else
            let unit = DashSpecFormatOptions.indentUnit options
            let rawLines = (normalizeNewlines text).Split('\n')
            let output = ResizeArray<string>(rawLines.Length)
            let stack = Stack<BlockFrame>()
            let mutable moduleStarted = false
            let mutable previousNonBlank = ""
            let mutable previousNonBlankIndent = 0

            let trackLine indentUnits trimmed =
                previousNonBlank <- trimmed
                previousNonBlankIndent <- indentUnits

            let emitOpenedLine indentUnits trimmed =
                maybeInsertBlankBetweenBlocks options previousNonBlank previousNonBlankIndent indentUnits output
                output.Add(pad indentUnits unit trimmed)
                trackLine indentUnits trimmed

            let emitClosedLine indentUnits trimmed =
                output.Add(pad indentUnits unit trimmed)
                trackLine indentUnits trimmed

            for rawLine in rawLines do
                if rawLine.Length = 0 then output.Add("")
                else
                    let trimmed = rawLine.Trim()
                    if trimmed.Length = 0 then output.Add("")
                    else
                        match BlockFormatterRules.classifyLine trimmed with
                        | BlockFormatterRules.Blank -> output.Add("")
                        | BlockFormatterRules.End _ ->
                            if options.DashSpecPreserveBlankLineBeforeEnd then
                                maybeInsertBlankBeforeEnd options previousNonBlank output
                            else
                                dropBlankLineBeforeEnd output

                            let endIndent =
                                if stack.Count > 0 then stack.Pop().EndIndent
                                elif moduleStarted then 1
                                else 0

                            emitClosedLine endIndent trimmed
                        | BlockFormatterRules.BraceClose ->
                            let closeIndent =
                                if stack.Count > 0 then stack.Pop().EndIndent
                                elif moduleStarted then 1
                                else 0

                            emitClosedLine closeIndent trimmed
                        | BlockFormatterRules.ModuleHeader _ ->
                            let indentUnits = resolveContentIndent stack moduleStarted
                            emitOpenedLine indentUnits trimmed
                            moduleStarted <- true
                            stack.Push({ EndIndent = indentUnits; ContentIndent = indentUnits + 1 })
                        | BlockFormatterRules.BlockOpener _ ->
                            let indentUnits = resolveContentIndent stack moduleStarted
                            emitOpenedLine indentUnits trimmed
                            stack.Push({ EndIndent = indentUnits; ContentIndent = indentUnits + 1 })
                        | BlockFormatterRules.BraceOpen ->
                            let indentUnits = resolveContentIndent stack moduleStarted
                            emitOpenedLine indentUnits trimmed
                            stack.Push({ EndIndent = indentUnits; ContentIndent = indentUnits + 1 })
                        | BlockFormatterRules.Content ->
                            let indentUnits = resolveContentIndent stack moduleStarted
                            emitOpenedLine indentUnits trimmed

            String.Join('\n', output)
