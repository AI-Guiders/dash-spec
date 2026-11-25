namespace DashSpec.Modeling.Parse.Hotkeys

open System
open System.Collections.Generic
open AIGuiders.Platform.Modeling.Notations.Keyboard
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Syntax

/// <summary>
/// Supported keyboard &amp; pointer gesture notation dialects in <c>module hotkeys &lt;id&gt;</c>
/// backed by <c>AIGuiders.Platform.Notations.Keyboard.*</c> (<c>NormalizedKeySequence</c>).
/// </summary>
[<RequireQualifiedAccess>]
type HotkeyNotationKind =
    | Auto
    | KeyGesture
    | Vim
    | NeovimKbd
    | EmacsKbd

module HotkeyNotationKind =

    let tryParse (raw: string) : HotkeyNotationKind option =
        match raw.Trim().ToLowerInvariant() with
        | "auto" -> Some HotkeyNotationKind.Auto
        | "key-gesture" | "keygesture" | "key_gesture" -> Some HotkeyNotationKind.KeyGesture
        | "vim" | "vim-doc" -> Some HotkeyNotationKind.Vim
        | "neovim-kbd" | "neovim" | "nvim" -> Some HotkeyNotationKind.NeovimKbd
        | "emacs-kbd" | "emacs" -> Some HotkeyNotationKind.EmacsKbd
        | _ -> None

    let toKeyword (kind: HotkeyNotationKind) : string =
        match kind with
        | HotkeyNotationKind.Auto -> "auto"
        | HotkeyNotationKind.KeyGesture -> "key-gesture"
        | HotkeyNotationKind.Vim -> "vim"
        | HotkeyNotationKind.NeovimKbd -> "neovim-kbd"
        | HotkeyNotationKind.EmacsKbd -> "emacs-kbd"

/// <summary>
/// Target interaction scope of a hotkey binding in <c>module hotkeys &lt;id&gt;</c>.
/// </summary>
[<RequireQualifiedAccess>]
type HotkeyScopeKind =
    | Host
    | Command
    | Diagram
    | Card
    | Filter
    | Table

module HotkeyScopeKind =

    let tryParse (raw: string) : HotkeyScopeKind option =
        match raw.Trim().ToLowerInvariant() with
        | "host" | "global" | "shell" -> Some HotkeyScopeKind.Host
        | "command" | "ccl" | "palette" -> Some HotkeyScopeKind.Command
        | "diagram" | "chart" | "heatmap" | "matrix" -> Some HotkeyScopeKind.Diagram
        | "card" | "viz" -> Some HotkeyScopeKind.Card
        | "filter" | "toolbar" -> Some HotkeyScopeKind.Filter
        | "table" | "grid" -> Some HotkeyScopeKind.Table
        | _ -> None

    let toKeyword (kind: HotkeyScopeKind) : string =
        match kind with
        | HotkeyScopeKind.Host -> "host"
        | HotkeyScopeKind.Command -> "command"
        | HotkeyScopeKind.Diagram -> "diagram"
        | HotkeyScopeKind.Card -> "card"
        | HotkeyScopeKind.Filter -> "filter"
        | HotkeyScopeKind.Table -> "table"

/// <summary>
/// One parsed and normalized hotkey/pointer gesture binding from <c>module hotkeys &lt;id&gt;</c>.
/// </summary>
[<CLIMutable>]
type HotkeyBindingDef =
    { Id: string
      RawGesture: string
      Notation: HotkeyNotationKind
      NormalizedSequence: NormalizedKeySequence
      CanonicalGesture: string
      CanonicalVimGesture: string
      Scope: HotkeyScopeKind
      Action: string
      Label: string option }

/// <summary>
/// Compiled <c>module hotkeys &lt;id&gt;</c> specification.
/// </summary>
[<CLIMutable>]
type HotkeysDocument =
    { Id: string
      DefaultNotation: HotkeyNotationKind
      ChordRoot: string option
      Bindings: IReadOnlyList<HotkeyBindingDef> }

/// <summary>
/// Multi-notation keyboard &amp; mouse gesture normalizer producing <see cref="NormalizedKeySequence"/>
/// across <c>key-gesture</c> (<c>Ctrl+K</c>, <c>Ctrl+LMB</c>), <c>vim</c> (<c>&lt;C-k&gt;</c>, <c>&lt;C-LMB&gt;</c>),
/// <c>neovim-kbd</c> (<c>&lt;Space&gt;</c>, <c>&lt;CR&gt;</c>, <c>&lt;Esc&gt;</c>), and <c>emacs-kbd</c> (<c>C-x C-f</c>).
/// Also supports a pluggable delegate bridge so <c>AIGuiders.Platform.Notations.Keyboard.All</c> can
/// register its full C# quarry parsers at startup.
/// </summary>
module HotkeyGestureNormalizer =

    type ExternalParserDelegate =
        delegate of string * HotkeyNotationKind * byref<NormalizedKeySequence> * byref<string> -> bool

    let mutable private externalParser: ExternalParserDelegate option = None

    /// Register the C# <c>AIGuiders.Platform.Notations.Keyboard.KeyboardNotationParser</c> bridge.
    let registerExternalParser (parser: ExternalParserDelegate) =
        externalParser <- Some parser

    let private normalizeKeyToken (rawKey: string) : string =
        let k = rawKey.Trim()
        if k.Length = 1 && Char.IsLetter k.[0] then
            k.ToUpperInvariant()
        else
            match k.ToUpperInvariant() with
            | "LMB" | "LEFTMOUSE" | "MOUSE1" -> "LMB"
            | "RMB" | "RIGHTMOUSE" | "MOUSE2" -> "RMB"
            | "MMB" | "MIDDLEMOUSE" | "MOUSE3" -> "MMB"
            | "SPACE" | "SPC" -> "Space"
            | "CR" | "RET" | "RETURN" | "ENTER" -> "Enter"
            | "ESC" | "ESCAPE" -> "Escape"
            | "TAB" -> "Tab"
            | "BS" | "BACKSPACE" -> "Backspace"
            | "DEL" | "DELETE" -> "Delete"
            | _ -> k

    let private tryParseBracketToken (token: string) : Microsoft.FSharp.Core.Result<NormalizedSequenceStep, string> =
        let t = token.Trim()
        if t.Length < 3 || t.[0] <> '<' || t.[t.Length - 1] <> '>' then
            Microsoft.FSharp.Core.Result.Error $"Expected '<...>' bracket token, got '{token}'."
        else
            let mutable inner = t.Substring(1, t.Length - 2)
            let mutable mods = ChordModifierKeys.None
            let mutable keepScanning = true
            while keepScanning && inner.Length > 0 do
                if inner.StartsWith("Alt-", StringComparison.OrdinalIgnoreCase) then
                    mods <- mods ||| ChordModifierKeys.Alt
                    inner <- inner.Substring(4)
                elif inner.StartsWith("Ctrl-", StringComparison.OrdinalIgnoreCase) then
                    mods <- mods ||| ChordModifierKeys.Control
                    inner <- inner.Substring(5)
                elif inner.StartsWith("Shift-", StringComparison.OrdinalIgnoreCase) then
                    mods <- mods ||| ChordModifierKeys.Shift
                    inner <- inner.Substring(6)
                elif inner.StartsWith("Meta-", StringComparison.OrdinalIgnoreCase) || inner.StartsWith("Cmd-", StringComparison.OrdinalIgnoreCase) then
                    mods <- mods ||| ChordModifierKeys.Meta
                    inner <- inner.Substring(if inner.StartsWith("Meta-", StringComparison.OrdinalIgnoreCase) then 5 else 4)
                elif inner.StartsWith("C-", StringComparison.OrdinalIgnoreCase) then
                    mods <- mods ||| ChordModifierKeys.Control
                    inner <- inner.Substring(2)
                elif inner.StartsWith("M-", StringComparison.OrdinalIgnoreCase) || inner.StartsWith("A-", StringComparison.OrdinalIgnoreCase) then
                    mods <- mods ||| ChordModifierKeys.Alt
                    inner <- inner.Substring(2)
                elif inner.StartsWith("S-", StringComparison.OrdinalIgnoreCase) then
                    mods <- mods ||| ChordModifierKeys.Shift
                    inner <- inner.Substring(2)
                elif inner.StartsWith("D-", StringComparison.OrdinalIgnoreCase) || inner.StartsWith("H-", StringComparison.OrdinalIgnoreCase) then
                    mods <- mods ||| ChordModifierKeys.Meta
                    inner <- inner.Substring(2)
                else
                    keepScanning <- false

            if String.IsNullOrWhiteSpace inner then
                Microsoft.FSharp.Core.Result.Error $"Empty key symbol inside '{token}'."
            else
                let normKey = normalizeKeyToken inner
                if mods = ChordModifierKeys.None then
                    Microsoft.FSharp.Core.Result.Ok(NormalizedPlainKeyStep(normKey) :> NormalizedSequenceStep)
                else
                    Microsoft.FSharp.Core.Result.Ok(NormalizedChordStep(mods, normKey) :> NormalizedSequenceStep)

    let private tryParseEmacsHyphenToken (token: string) : Microsoft.FSharp.Core.Result<NormalizedSequenceStep, string> =
        let mutable inner = token.Trim()
        if inner.Length >= 2 && inner.[0] = '<' && inner.[inner.Length - 1] = '>' then
            inner <- inner.Substring(1, inner.Length - 2)
        let mutable mods = ChordModifierKeys.None
        let mutable keepScanning = true
        while keepScanning && inner.Length > 0 do
            if inner.StartsWith("C-", StringComparison.Ordinal) then
                mods <- mods ||| ChordModifierKeys.Control
                inner <- inner.Substring(2)
            elif inner.StartsWith("M-", StringComparison.Ordinal) || inner.StartsWith("A-", StringComparison.Ordinal) then
                mods <- mods ||| ChordModifierKeys.Alt
                inner <- inner.Substring(2)
            elif inner.StartsWith("S-", StringComparison.Ordinal) then
                mods <- mods ||| ChordModifierKeys.Shift
                inner <- inner.Substring(2)
            elif inner.StartsWith("s-", StringComparison.Ordinal) || inner.StartsWith("H-", StringComparison.Ordinal) || inner.StartsWith("D-", StringComparison.Ordinal) then
                mods <- mods ||| ChordModifierKeys.Meta
                inner <- inner.Substring(2)
            else
                keepScanning <- false

        if String.IsNullOrWhiteSpace inner then
            Microsoft.FSharp.Core.Result.Error $"Empty key symbol after modifiers in '{token}'."
        else
            let normKey = normalizeKeyToken inner
            if mods = ChordModifierKeys.None then
                Microsoft.FSharp.Core.Result.Ok(NormalizedPlainKeyStep(normKey) :> NormalizedSequenceStep)
            else
                Microsoft.FSharp.Core.Result.Ok(NormalizedChordStep(mods, normKey) :> NormalizedSequenceStep)

    let private tryParseKeyGestureToken (token: string) : Microsoft.FSharp.Core.Result<NormalizedSequenceStep, string> =
        let t = token.Trim()
        if String.IsNullOrWhiteSpace t then
            Microsoft.FSharp.Core.Result.Error "Empty gesture token."
        elif t.Length >= 2 && t.[0] = '\u2318' then
            let key = normalizeKeyToken (t.Substring(1))
            if String.IsNullOrWhiteSpace key then
                Microsoft.FSharp.Core.Result.Error $"Empty key after ⌘ in '{token}'."
            else
                Microsoft.FSharp.Core.Result.Ok(NormalizedChordStep(ChordModifierKeys.Meta, key) :> NormalizedSequenceStep)
        elif not (t.Contains('+')) then
            if t.EndsWith('-') && t.Length > 1 then
                Microsoft.FSharp.Core.Result.Error $"Malformed gesture token '{token}'."
            else
                Microsoft.FSharp.Core.Result.Ok(NormalizedPlainKeyStep(normalizeKeyToken t) :> NormalizedSequenceStep)
        else
            let rawParts = t.Split('+') |> Array.map (fun p -> p.Trim())
            if rawParts |> Array.exists String.IsNullOrWhiteSpace || rawParts.Length < 2 then
                Microsoft.FSharp.Core.Result.Error $"Expected 'Modifier+Key' in '{token}'."
            else
                let mutable mods = ChordModifierKeys.None
                let mutable err: string option = None
                for i in 0 .. rawParts.Length - 2 do
                    match rawParts.[i].ToUpperInvariant() with
                    | "CTRL" | "CONTROL" | "C" -> mods <- mods ||| ChordModifierKeys.Control
                    | "ALT" | "OPTION" | "A" | "M" -> mods <- mods ||| ChordModifierKeys.Alt
                    | "SHIFT" | "S" -> mods <- mods ||| ChordModifierKeys.Shift
                    | "META" | "CMD" | "COMMAND" | "WIN" | "SUPER" | "D" | "\u2318" -> mods <- mods ||| ChordModifierKeys.Meta
                    | unknown -> err <- Some $"Unknown modifier '{unknown}' in '{token}'."
                match err with
                | Some e -> Microsoft.FSharp.Core.Result.Error e
                | None ->
                    let key = normalizeKeyToken rawParts.[rawParts.Length - 1]
                    Microsoft.FSharp.Core.Result.Ok(NormalizedChordStep(mods, key) :> NormalizedSequenceStep)

    let private collapseSpacesAroundPlus (wire: string) : string =
        if not (wire.Contains('+')) then
            wire
        else
            wire.Split('+')
            |> Array.map (fun p -> p.Trim())
            |> String.concat "+"

    let private tryParseTokenByNotation (notation: HotkeyNotationKind) (token: string) : Microsoft.FSharp.Core.Result<NormalizedSequenceStep, string> =
        let t = token.Trim()
        match notation with
        | HotkeyNotationKind.Vim
        | HotkeyNotationKind.NeovimKbd ->
            if t.StartsWith('<') || t.EndsWith('>') then
                tryParseBracketToken t
            elif t.Contains('+') then
                Microsoft.FSharp.Core.Result.Error $"Notation '{HotkeyNotationKind.toKeyword notation}' uses '<C-k>' bracket syntax, not '+' ('{token}')."
            else
                Microsoft.FSharp.Core.Result.Ok(NormalizedPlainKeyStep(normalizeKeyToken t) :> NormalizedSequenceStep)
        | HotkeyNotationKind.EmacsKbd ->
            if t.Contains('+') then
                Microsoft.FSharp.Core.Result.Error $"Notation 'emacs-kbd' uses 'C-k' hyphen syntax, not '+' ('{token}')."
            else
                tryParseEmacsHyphenToken t
        | HotkeyNotationKind.KeyGesture ->
            if t.StartsWith('<') || t.EndsWith('>') then
                Microsoft.FSharp.Core.Result.Error $"Notation 'key-gesture' uses 'Ctrl+K' syntax, not '<...>' ('{token}')."
            else
                tryParseKeyGestureToken t
        | HotkeyNotationKind.Auto ->
            if t.StartsWith('<') || t.EndsWith('>') then
                tryParseBracketToken t
            elif t.Contains('+') || t.StartsWith('\u2318') then
                tryParseKeyGestureToken t
            elif t.StartsWith("C-", StringComparison.Ordinal)
                 || t.StartsWith("M-", StringComparison.Ordinal)
                 || t.StartsWith("S-", StringComparison.Ordinal)
                 || t.StartsWith("A-", StringComparison.Ordinal)
                 || t.StartsWith("H-", StringComparison.Ordinal)
                 || t.StartsWith("s-", StringComparison.Ordinal)
                 || t.StartsWith("D-", StringComparison.Ordinal) then
                tryParseEmacsHyphenToken t
            else
                tryParseKeyGestureToken t

    /// Format a <see cref="NormalizedKeySequence"/> into canonical KeyGesture wire format (e.g. <c>Ctrl+K</c>, <c>Ctrl+LMB</c>).
    let toCanonicalKeyGesture (seq: NormalizedKeySequence) : string =
        seq.Steps
        |> Seq.map (fun step ->
            match step with
            | :? NormalizedChordStep as chord ->
                let parts = ResizeArray<string>(5)
                if chord.Modifiers.HasFlag(ChordModifierKeys.Control) then parts.Add "Ctrl"
                if chord.Modifiers.HasFlag(ChordModifierKeys.Alt) then parts.Add "Alt"
                if chord.Modifiers.HasFlag(ChordModifierKeys.Shift) then parts.Add "Shift"
                if chord.Modifiers.HasFlag(ChordModifierKeys.Meta) then parts.Add "Meta"
                parts.Add(normalizeKeyToken chord.KeySymbol)
                String.Join("+", parts)
            | :? NormalizedPlainKeyStep as plain ->
                normalizeKeyToken plain.KeySymbol
            | _ -> "")
        |> String.concat " "

    /// Format a <see cref="NormalizedKeySequence"/> into canonical Vim bracket notation (e.g. <c>&lt;C-k&gt;</c>, <c>&lt;C-LMB&gt;</c>).
    let toCanonicalVimNotation (seq: NormalizedKeySequence) : string =
        seq.Steps
        |> Seq.map (fun step ->
            match step with
            | :? NormalizedChordStep as chord ->
                let sb = System.Text.StringBuilder("<")
                if chord.Modifiers.HasFlag(ChordModifierKeys.Control) then sb.Append("C-") |> ignore
                if chord.Modifiers.HasFlag(ChordModifierKeys.Alt) then sb.Append("M-") |> ignore
                if chord.Modifiers.HasFlag(ChordModifierKeys.Shift) then sb.Append("S-") |> ignore
                if chord.Modifiers.HasFlag(ChordModifierKeys.Meta) then sb.Append("D-") |> ignore
                let k = normalizeKeyToken chord.KeySymbol
                let vimKey =
                    if k.Length = 1 && Char.IsLetter k.[0] && not (chord.Modifiers.HasFlag(ChordModifierKeys.Shift)) then
                        k.ToLowerInvariant()
                    else
                        k
                sb.Append(vimKey).Append('>') |> ignore
                sb.ToString()
            | :? NormalizedPlainKeyStep as plain ->
                let k = normalizeKeyToken plain.KeySymbol
                if k.Length > 1 && not (String.Equals(k, "LMB", StringComparison.OrdinalIgnoreCase))
                                && not (String.Equals(k, "RMB", StringComparison.OrdinalIgnoreCase))
                                && not (String.Equals(k, "MMB", StringComparison.OrdinalIgnoreCase)) then
                    $"<{k}>"
                else
                    k
            | _ -> "")
        |> String.concat " "

    /// Parse a gesture wire string in the specified <see cref="HotkeyNotationKind"/> into <see cref="NormalizedKeySequence"/>.
    let tryParseGesture (notation: HotkeyNotationKind) (rawWire: string) : Microsoft.FSharp.Core.Result<NormalizedKeySequence, string> =
        if String.IsNullOrWhiteSpace rawWire then
            Microsoft.FSharp.Core.Result.Error "Gesture cannot be empty."
        else
            let cleaned =
                let trimmed = rawWire.Trim()
                if notation = HotkeyNotationKind.KeyGesture || (notation = HotkeyNotationKind.Auto && trimmed.Contains('+')) then
                    collapseSpacesAroundPlus trimmed
                else
                    trimmed

            let extResult =
                match externalParser with
                | Some ext ->
                    let mutable seqOut = NormalizedKeySequence.Empty
                    let mutable errOut = ""
                    if ext.Invoke(cleaned, notation, &seqOut, &errOut) && not (isNull (box seqOut)) && seqOut.Steps.Count > 0 then
                        let normSteps =
                            seqOut.Steps
                            |> Seq.map (fun s ->
                                match s with
                                | :? NormalizedChordStep as ch ->
                                    NormalizedChordStep(ch.Modifiers, normalizeKeyToken ch.KeySymbol) :> NormalizedSequenceStep
                                | :? NormalizedPlainKeyStep as pl ->
                                    NormalizedPlainKeyStep(normalizeKeyToken pl.KeySymbol) :> NormalizedSequenceStep
                                | other -> other)
                            |> Seq.toArray
                        Some(Microsoft.FSharp.Core.Result.Ok(NormalizedKeySequence(normSteps)))
                    else
                        None
                | None -> None

            match extResult with
            | Some ok -> ok
            | None ->
                let tokens = cleaned.Split((null: char[]), StringSplitOptions.RemoveEmptyEntries)
                if tokens.Length = 0 then
                    Microsoft.FSharp.Core.Result.Error "Gesture cannot be empty."
                else
                    let steps = ResizeArray<NormalizedSequenceStep>(tokens.Length)
                    let mutable firstErr: string option = None
                    for tok in tokens do
                        if firstErr.IsNone then
                            match tryParseTokenByNotation notation tok with
                            | Microsoft.FSharp.Core.Result.Ok step -> steps.Add step
                            | Microsoft.FSharp.Core.Result.Error e -> firstErr <- Some e
                    match firstErr with
                    | Some e -> Microsoft.FSharp.Core.Result.Error e
                    | None -> Microsoft.FSharp.Core.Result.Ok(NormalizedKeySequence(steps.ToArray()))

module HotkeysModuleParser =

    let private hotkeyDeclaration
        (defaultNotationRef: unit -> HotkeyNotationKind)
        (addRow: HotkeyBindingDef -> unit)
        : Construct.Declaration<HotkeyBindingDef> =
        { Name = "hotkeys"
          Vocabulary = [ "id"; "name"; "gesture"; "key"; "chord"; "notation"; "scope"; "action"; "command"; "target"; "label"; "title" ]
          CellsToRow = fun m ->
              let id =
                  Construct.tryCell m "id"
                  |> Option.orElse (Construct.tryCell m "name")
                  |> Option.map Construct.unquote
                  |> Option.defaultWith (fun () ->
                      raise (DashSpecMissingMemberException("id", "hotkeys table row requires an 'id' (or 'name') cell.")))

              let rawGesture =
                  Construct.tryCell m "gesture"
                  |> Option.orElse (Construct.tryCell m "key")
                  |> Option.orElse (Construct.tryCell m "chord")
                  |> Option.map Construct.unquote
                  |> Option.defaultWith (fun () ->
                      raise (DashSpecMissingMemberException("gesture", $"hotkey '{id}' requires a 'gesture' cell.")))

              let rowNotation =
                  match Construct.tryCell m "notation" |> Option.map Construct.unquote with
                  | Some raw when not (String.IsNullOrWhiteSpace raw) && raw <> "-" ->
                      HotkeyNotationKind.tryParse raw
                      |> Option.defaultWith (fun () ->
                          raise (DashSpecInvalidValueException($"hotkey '{id}': unknown notation '{raw}'. Expected auto, key-gesture, vim, neovim-kbd, or emacs-kbd.")))
                  | _ -> defaultNotationRef ()

              let rawScope =
                  Construct.tryCell m "scope"
                  |> Option.map Construct.unquote
                  |> Option.defaultValue "host"

              let scope =
                  HotkeyScopeKind.tryParse rawScope
                  |> Option.defaultWith (fun () ->
                      raise (DashSpecInvalidValueException($"hotkey '{id}': unknown scope '{rawScope}'. Expected host, command, diagram, card, filter, or table.")))

              let action =
                  Construct.tryCell m "action"
                  |> Option.orElse (Construct.tryCell m "command")
                  |> Option.orElse (Construct.tryCell m "target")
                  |> Option.map Construct.unquote
                  |> Option.defaultWith (fun () ->
                      raise (DashSpecMissingMemberException("action", $"hotkey '{id}' requires an 'action' (or 'command'/'target') cell.")))

              let label =
                  Construct.tryCell m "label"
                  |> Option.orElse (Construct.tryCell m "title")
                  |> Option.map Construct.unquote
                  |> Option.bind (fun s -> if s = "-" || String.IsNullOrWhiteSpace s then None else Some s)

              let normalizedSeq =
                  match HotkeyGestureNormalizer.tryParseGesture rowNotation rawGesture with
                  | Microsoft.FSharp.Core.Result.Ok seq -> seq
                  | Microsoft.FSharp.Core.Result.Error err ->
                      raise (
                          DashSpecInvalidValueException(
                              $"hotkey '{id}': invalid gesture '{rawGesture}' for notation '{HotkeyNotationKind.toKeyword rowNotation}' ({err})."
                          )
                      )

              { Id = id.Trim()
                RawGesture = rawGesture.Trim()
                Notation = rowNotation
                NormalizedSequence = normalizedSeq
                CanonicalGesture = HotkeyGestureNormalizer.toCanonicalKeyGesture normalizedSeq
                CanonicalVimGesture = HotkeyGestureNormalizer.toCanonicalVimNotation normalizedSeq
                Scope = scope
                Action = action.Trim()
                Label = label }
          ConstructRow = addRow }

    let parseMembers (moduleId: string) (members: DashSpecAstNode[]) : HotkeysDocument =
        let mutable defaultNotation = HotkeyNotationKind.Auto
        let mutable chordRoot: string option = None

        for node in members do
            match node with
            | DashSpecAstNode.PropertyAssignment p when not p.KeyPath.IsQualified ->
                match p.Key.Trim().ToLowerInvariant() with
                | "notation" ->
                    let raw = Construct.unquote p.ValueText
                    defaultNotation <-
                        HotkeyNotationKind.tryParse raw
                        |> Option.defaultWith (fun () ->
                            raise (DashSpecInvalidValueException($"module hotkeys '{moduleId}': unknown notation '{raw}'. Expected auto, key-gesture, vim, neovim-kbd, or emacs-kbd.", "notation", p.Span.Start)))
                | "chord_root" ->
                    let raw = Construct.unquote p.ValueText
                    match HotkeyGestureNormalizer.tryParseGesture defaultNotation raw with
                    | Microsoft.FSharp.Core.Result.Ok seq ->
                        chordRoot <- Some(HotkeyGestureNormalizer.toCanonicalKeyGesture seq)
                    | Microsoft.FSharp.Core.Result.Error err ->
                        raise (DashSpecInvalidValueException($"module hotkeys '{moduleId}': invalid chord_root '{raw}' ({err}).", "chord_root", p.Span.Start))
                | unknown ->
                    raise (DashSpecUnknownPropertyException(unknown, $"module hotkeys '{moduleId}': unknown property '{unknown}'. Expected 'notation' or 'chord_root'.", p.Span.Start))
            | _ -> ()

        let rows = ResizeArray<HotkeyBindingDef>()
        let decl = hotkeyDeclaration (fun () -> defaultNotation) rows.Add

        Construct.projectAst
            decl
            (Some(DashSpecBlockKeyword.Other "hotkey"))
            "id"
            members

        for node in members do
            match node with
            | DashSpecAstNode.BlockDeclaration b ->
                match b.Opener with
                | DashSpecBlockOpener.Anonymous(DashSpecBlockKeyword.Other kw)
                    when String.Equals(kw, "bindings", StringComparison.OrdinalIgnoreCase)
                         || String.Equals(kw, "hotkeys", StringComparison.OrdinalIgnoreCase) ->
                    Construct.projectAst
                        decl
                        (Some(DashSpecBlockKeyword.Other "hotkey"))
                        "id"
                        b.Members
                | _ -> ()
            | _ -> ()

        if rows.Count = 0 then
            raise (DashSpecParseException($"module hotkeys '{moduleId}' requires at least one hotkey binding."))

        let resolvedChordRoot =
            chordRoot
            |> Option.orElseWith (fun () ->
                rows
                |> Seq.tryFind (fun r ->
                    String.Equals(r.Id, "chord_root", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(r.Action, "command.chord_root", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(r.Action, "chord-root", StringComparison.OrdinalIgnoreCase))
                |> Option.map (fun r -> r.CanonicalGesture))

        { Id = moduleId
          DefaultNotation = defaultNotation
          ChordRoot = resolvedChordRoot
          Bindings = rows.ToArray() :> IReadOnlyList<_> }

    let parseEnvelope (env: UnitGrammar.UnitEnvelope) : HotkeysDocument =
        parseMembers env.Id env.Body

    let parse (text: string) : HotkeysDocument =
        UnitGrammar.parseUnit DashSpecModuleKind.Hotkeys parseEnvelope text
