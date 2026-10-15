namespace DashSpec.Modeling.Parse.Syntax

open System
open System.Collections.Generic
open DashSpec.Modeling.Core

/// <summary>One language rule violation found on the surface tree.</summary>
type DashSpecRuleFinding =
    { Rule: DashSpecRule
      Message: string
      Span: TextSpan }

/// <summary>Language rules evaluated in one document-order pass over the surface tree (ADR-0102).</summary>
module DashSpecRuleChecks =

    type private BlockFrame =
        { Keyword: DashSpecBlockKeyword
          Identifier: string option
          Span: TextSpan }

    let private moduleFrame (directive: DashSpecModuleDirective) (span: TextSpan) : BlockFrame =
        match directive with
        | DashSpecModuleDirective.Dashboard id ->
            { Keyword = DashSpecBlockKeyword.Other "dashboard"
              Identifier = Some id
              Span = span }
        | DashSpecModuleDirective.Tab id ->
            { Keyword = DashSpecBlockKeyword.Tab
              Identifier = Some id
              Span = span }
        | DashSpecModuleDirective.Unit(keyword, id) ->
            { Keyword = DashSpecBlockKeyword.Other keyword
              Identifier = Some id
              Span = span }

    let private blockFrame (opener: DashSpecBlockOpener) (span: TextSpan) : BlockFrame =
        match opener with
        | DashSpecBlockOpener.Named(keyword, identifier) ->
            { Keyword = keyword
              Identifier = Some identifier
              Span = span }
        | DashSpecBlockOpener.Anonymous keyword ->
            { Keyword = keyword
              Identifier = None
              Span = span }

    /// Tab blocks without a matching `end tab` in their subtree are registry headers, not balance frames.
    let private participatesInBlockBalance (node: DashSpecAstNode) =
        match node with
        | DashSpecAstNode.BlockDeclaration n ->
            match n.Opener with
            | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Tab, id) ->
                DashSpecAst.hasClosingEndBlock node DashSpecBlockKeyword.Tab (Some id)
            | _ -> true
        | _ -> true

    let private opensCards (node: DashSpecAstNode) =
        match node with
        | DashSpecAstNode.BlockDeclaration n ->
            match n.Opener with
            | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Cards, _) -> true
            | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Cards -> true
            | _ -> false
        | _ -> false

    /// <summary>Zero-based line and character for a source offset.</summary>
    let positionOf (text: string) (offset: int) : int * int =
        let limit = min (max offset 0) text.Length
        let mutable line = 0
        let mutable lineStart = 0

        for i in 0 .. limit - 1 do
            if text.[i] = '\n' then
                line <- line + 1
                lineStart <- i + 1

        line, limit - lineStart

    let findings (root: DashSpecAstNode) : DashSpecRuleFinding list =
        let violations = ResizeArray<DashSpecRuleFinding>()
        let blockStack = Stack<BlockFrame>()
        let cardsDepth = ref 0

        let signal (rule: DashSpecRule) (message: string) (span: TextSpan) =
            let finding: DashSpecRuleFinding =
                { Rule = rule
                  Message = message
                  Span = span }

            violations.Add finding

        let rec walk (node: DashSpecAstNode) =
            match node with
            | DashSpecAstNode.CardReference n when cardsDepth.Value = 0 ->
                signal DashSpecRule.CardReferenceOutsideCards $"card reference '{n.CardId}' is not nested under a cards block" n.Span
            | DashSpecAstNode.ModuleDeclaration n ->
                blockStack.Push(moduleFrame n.Directive n.Span)
                let mutable seenCode = false

                for child in DashSpecAst.members node do
                    match child with
                    | DashSpecAstNode.ImportDeclaration import when seenCode ->
                        signal DashSpecRule.ImportAfterCode $"'{import.Keyword}' belongs in the module header, before code sections (ADR-0101)." import.Span
                    | DashSpecAstNode.ImportDeclaration _
                    | DashSpecAstNode.BlankLine _ -> ()
                    | _ -> seenCode <- true

                    walk child
            | DashSpecAstNode.BlockDeclaration n ->
                if participatesInBlockBalance node then
                    blockStack.Push(blockFrame n.Opener n.Span)

                let cards = opensCards node

                if cards then
                    cardsDepth.Value <- cardsDepth.Value + 1

                for child in DashSpecAst.members node do
                    walk child

                if cards then
                    cardsDepth.Value <- cardsDepth.Value - 1
            | DashSpecAstNode.EndBlock n ->
                if blockStack.Count = 0 then
                    signal DashSpecRule.ExtraEndBlock "unexpected end block" n.Span
                else
                    let frame = blockStack.Pop()

                    if frame.Keyword <> n.EndKeyword then
                        signal
                            DashSpecRule.MismatchedEndKeyword
                            $"end {DashSpecBlockKeyword.toEndName n.EndKeyword} does not match opener {DashSpecBlockKeyword.toEndName frame.Keyword}"
                            n.Span

                    match frame.Identifier, n.EndId with
                    | Some openerId, Some endId when not (String.Equals(openerId, endId, StringComparison.Ordinal)) ->
                        signal DashSpecRule.MismatchedEndIdentifier $"end block identifier '{endId}' does not match opener '{openerId}'" n.Span
                    | _ -> ()
            | _ ->
                for child in DashSpecAst.members node do
                    walk child

        walk root

        if blockStack.Count > 0 then
            let frame = blockStack.Peek()
            signal DashSpecRule.UnclosedBlock $"unclosed block '{DashSpecBlockKeyword.toEndName frame.Keyword}'" frame.Span

        List.ofSeq violations

    let toDiagnostic (text: string) (finding: DashSpecRuleFinding) : DashSpecDiagnostic =
        let line, character = positionOf text finding.Span.Start
        let endLine, endCharacter = positionOf text finding.Span.End

        DashSpecDiagnostic.create line character endLine endCharacter finding.Message (DashSpecRules.severity finding.Rule)
        |> DashSpecDiagnostic.withCode (DashSpecRules.code finding.Rule)
