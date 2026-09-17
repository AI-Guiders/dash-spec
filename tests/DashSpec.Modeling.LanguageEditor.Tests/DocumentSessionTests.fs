namespace DashSpec.Modeling.LanguageEditor.Tests

open Xunit
open DashSpec.Modeling.LanguageEditor

module DocumentSessionTests =

    [<Fact>]
    let ``sync bumps revision and classification follows`` () =
        let session = DocumentSession.Create("doc://demo.dashspec", "@dashboard demo\nend dashboard\n")
        Assert.Equal(0, session.Revision)
        let spans0 = session.GetClassificationSpans()
        Assert.NotEmpty(spans0)
        session.SyncFromText("@dashboard demo\nruntime\nend runtime\nend dashboard\n")
        Assert.Equal(1, session.Revision)
        Assert.True(session.GetClassificationSpans().Count > spans0.Count)

    [<Fact>]
    let ``caret resolves syntax node`` () =
        let session = DocumentSession.Create("doc://demo.dashspec", "    tab x as \"T\"\n")
        match session.TryResolveCaret(8) with
        | None -> Assert.Fail("expected syntax locus")
        | Some locus ->
            Assert.Equal("Syntax", locus.Tier)
            Assert.True(locus.Start <= 8 && locus.End > 8)
