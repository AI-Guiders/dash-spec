namespace DashSpec.Modeling.Parse.Toolbar

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Syntax

[<CLIMutable>]
type CommandBindingDef =
    { Alias: string
      Target: string
      Shortcut: string option
      Label: string option }

module CommandAliasesParser =

    let private commandDeclaration (addRow: CommandBindingDef -> unit) : Construct.Declaration<CommandBindingDef> =
        { Name = "commands"
          Vocabulary = [ "id"; "alias"; "name"; "target"; "filter"; "command"; "shortcut"; "melody"; "gesture"; "label"; "title" ]
          CellsToRow = fun m ->
              let aliasName =
                  Construct.tryCell m "alias"
                  |> Option.orElse (Construct.tryCell m "id")
                  |> Option.orElse (Construct.tryCell m "name")
                  |> Option.map Construct.unquote
                  |> Option.defaultWith (fun () ->
                      raise (DashSpecMissingMemberException("alias", "commands table row requires an 'alias' (or 'id'/'name') cell.")))

              let target =
                  Construct.tryCell m "target"
                  |> Option.orElse (Construct.tryCell m "filter")
                  |> Option.orElse (Construct.tryCell m "command")
                  |> Option.map Construct.unquote
                  |> Option.defaultWith (fun () ->
                      raise (DashSpecMissingMemberException("target", $"commands row '{aliasName}' requires a 'target' (or 'filter'/'command') cell.")))

              let shortcut =
                  Construct.tryCell m "shortcut"
                  |> Option.orElse (Construct.tryCell m "melody")
                  |> Option.orElse (Construct.tryCell m "gesture")
                  |> Option.map Construct.unquote

              let label =
                  Construct.tryCell m "label"
                  |> Option.orElse (Construct.tryCell m "title")
                  |> Option.map Construct.unquote

              { Alias = aliasName.Trim()
                Target = target.Trim()
                Shortcut = shortcut
                Label = label }
          ConstructRow = addRow }

        /// Project a `commands … end commands` block or `module commands <id>` body from AST members via Tier 2 (`Construct.Declaration<CommandBindingDef>`).
    let projectBindingsFromAst (members: DashSpecAstNode[]) : CommandBindingDef[] =
        let rows = ResizeArray<CommandBindingDef>()
        let decl = commandDeclaration rows.Add

        let tryProjectLine (node: DashSpecAstNode) : (string * string) seq option =
            match node with
            | DashSpecAstNode.PropertyAssignment p when not p.KeyPath.IsQualified ->
                Some(seq { yield "alias", p.Key; yield "target", Construct.unquote p.ValueText })
            | _ -> None

        Construct.projectAstWithLines
            decl
            (Some(DashSpecBlockKeyword.Other "command"))
            "alias"
            (Some tryProjectLine)
            members

        for node in members do
            match node with
            | DashSpecAstNode.BlockDeclaration b when b.Opener = DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Commands ->
                Construct.projectAstWithLines
                    decl
                    (Some(DashSpecBlockKeyword.Other "command"))
                    "alias"
                    (Some tryProjectLine)
                    b.Members
            | _ -> ()

        rows.ToArray()

    let private parseModuleDeclaration (members: DashSpecAstNode[]) =
        let bindings = projectBindingsFromAst members
        let result = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        for row in bindings do
            result.[row.Alias] <- row.Target
        result :> IReadOnlyDictionary<_, _>

    let projectFromAst (members: DashSpecAstNode[]) : IReadOnlyDictionary<string, string> =
        let result = parseModuleDeclaration members
        if result.Count = 0 then
            raise (DashSpecParseException("commands block requires at least one entry."))
        result

    let parseEnvelope (env: UnitGrammar.UnitEnvelope) : IReadOnlyDictionary<string, string> =
        parseModuleDeclaration env.Body

    let parseModule (text: string) : IReadOnlyDictionary<string, string> =
        UnitGrammar.parseUnit (DashSpecModuleKind.Custom "commands") parseEnvelope text

    let parse (reader: TokenReader) : IReadOnlyDictionary<string, string> =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let lines = ResizeArray<string>()

        while not reader.IsEof && not (BlockSyntax.isBlockEnd reader "commands" None) do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "commands" None then ()
            elif reader.CurrentKind = TokenKind.TableRow then
                lines.Add(reader.CurrentTableRow())
                reader.Advance()
            else
                let sb = System.Text.StringBuilder()
                while not (reader.IsOnNewline()) && reader.RawKind <> TokenKind.Eof && not (BlockSyntax.isBlockEnd reader "commands" None) do
                    let cur = reader.Current
                    if sb.Length > 0 && cur.Kind <> TokenKind.Dot && cur.Kind <> TokenKind.Colon then
                        sb.Append(' ') |> ignore
                    if cur.Kind = TokenKind.String then
                        sb.Append('"').Append(cur.Value).Append('"') |> ignore
                    else
                        sb.Append(cur.Value) |> ignore
                    reader.Advance()
                if sb.Length > 0 then
                    lines.Add(sb.ToString())
            reader.SkipNewlines()

        BlockSyntax.expectBlockEnd reader "commands" None
        let body = String.Join("\n", lines)
        let tree = DashSpecSurfaceSyntax.parse $"commands\n{body}\nend commands"
        let members =
            match tree.Root with
            | DashSpecAstNode.CompilationUnit cu ->
                cu.Members
                |> Array.tryPick (function
                    | DashSpecAstNode.BlockDeclaration b when b.Opener = DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Commands ->
                        Some b.Members
                    | _ -> None)
                |> Option.defaultValue cu.Members
            | other -> DashSpecAst.members other
        projectFromAst members
