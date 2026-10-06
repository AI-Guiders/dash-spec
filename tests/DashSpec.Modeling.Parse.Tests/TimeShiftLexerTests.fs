namespace DashSpec.Modeling.Parse.Tests

open Xunit
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Syntax

module TimeShiftLexerTests =

    [<Theory>]
    [<InlineData("UTC", "UTC")>]
    [<InlineData("UTC+3", "UTC+3")>]
    [<InlineData("utc+03:30", "utc+03:30")>]
    [<InlineData("UTC-5", "UTC-5")>]
    let ``lexer emits TimeShift for utc offset literals`` (snippet, expected) =
        let tokens = DashSpecLexer.tokenize snippet |> Seq.filter (fun t -> t.Kind <> TokenKind.Newline) |> Seq.toList
        let shift = tokens |> List.find (fun t -> t.Kind = TokenKind.TimeShift)
        Assert.Equal(expected, shift.Value)

    [<Fact>]
    let ``lexer emits IanaZone for region slash city`` () =
        let tokens =
            DashSpecLexer.tokenize "Europe/Moscow"
            |> Seq.filter (fun t -> t.Kind <> TokenKind.Newline)
            |> Seq.toList

        let zone =
            tokens
            |> List.find (fun t -> t.Kind = TokenKind.IanaZone)

        Assert.Equal("Europe/Moscow", zone.Value)

    [<Fact>]
    let ``syntax classifier colors TimeShift as number`` () =
        let kinds =
            DashSpecSyntaxClassifier.classify "zone = UTC+3\n"
            |> Seq.map (fun s -> s.Kind)
            |> Seq.toList

        Assert.True(List.contains DashSpecSyntaxKind.Number kinds)
