namespace DashSpec.Modeling.Parse.Tests

open Xunit
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module AccessorGrammarTests =

    [<Fact>]
    let ``lexer emits dot tokens inside view names`` () =
        let tokens = DashSpecLexer.tokenize "lus.v_daily_peak"
        Assert.Equal(TokenKind.Ident, tokens.[0].Kind)
        Assert.Equal("lus", tokens.[0].Value)
        Assert.Equal(TokenKind.Dot, tokens.[1].Kind)
        Assert.Equal(TokenKind.Ident, tokens.[2].Kind)
        Assert.Equal("v_daily_peak", tokens.[2].Value)

    [<Fact>]
    let ``accessor reads multi segment names`` () =
        let reader = ParserUtilities.createReader "schema.view.name"
        let accessor = AccessorGrammar.read reader
        Assert.Equal(3, accessor.SegmentCount)
        Assert.Equal("schema.view.name", accessor.Dotted)

    [<Fact>]
    let ``accessor is left associative select chain`` () =
        let reader = ParserUtilities.createReader "a.b"
        let accessor = AccessorGrammar.read reader

        match accessor with
        | AccessorGrammar.Accessor.Select(AccessorGrammar.Accessor.Name "a", "b") -> ()
        | _ -> Assert.Fail("expected Name 'a' . 'b'")

    [<Fact>]
    let ``flow producer port ref is graph endpoint`` () =
        let reader = ParserUtilities.createReader "from utilization.utilization"
        let portRef = DashSpec.Modeling.Parse.DataFlow.FlowPortRefParser.readProducerAfterFrom reader
        Assert.Equal("utilization", portRef.NodeId)
        Assert.Equal("utilization", portRef.PortName)
        Assert.Equal("utilization.utilization", FlowNodePortRef.dotted portRef)
