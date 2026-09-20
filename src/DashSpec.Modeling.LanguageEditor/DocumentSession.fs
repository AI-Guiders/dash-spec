namespace DashSpec.Modeling.LanguageEditor

open System.Collections.Generic
open DashSpec.Modeling.Parse.Syntax

type SessionClassificationSpan =
    { Start: int
      Length: int
      Kind: string }

type SessionLocus =
    { Start: int
      End: int
      Tier: string
      SymbolId: string option }

[<Sealed>]
type DocumentSession(documentId: string, initialText: string) =
    let mutable revision = 0
    let mutable text = initialText

    member _.DocumentId = documentId
    member _.Revision = revision
    member _.Text = text

    member _.SyncFromText(newText: string) =
        if not (System.String.Equals(text, newText, System.StringComparison.Ordinal)) then
            text <- newText
            revision <- revision + 1

    member _.GetClassificationSpans() : IReadOnlyList<SessionClassificationSpan> =
        SyntaxTree.parse text
        |> SyntaxTree.classifiedSpans
        |> Seq.map (fun span -> { Start = span.Start; Length = span.Length; Kind = span.Kind.ToString() })
        |> Seq.toList
        :> IReadOnlyList<_>

    member _.TryResolveCaret(offset: int) =
        match SyntaxTree.findNodeAt (SyntaxTree.parse text) offset with
        | None -> None
        | Some node ->
            let span = DashSpecAst.span node

            Some
                { Start = span.Start
                  End = span.End
                  Tier = "Syntax"
                  SymbolId = Some(DashSpecAst.outlineLabel node) }

    static member Create(documentId: string, text: string) = DocumentSession(documentId, text)
