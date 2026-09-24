namespace DashSpec.Modeling.Core.Tests

open Xunit
open DashSpec.Modeling.Core

type DiagnosticsTests() =
    [<Fact>]
    member _.``Round_trip_preserves_legacy_diagnostic_fields`` () =
        let legacy =
            DashSpec.Core.Validation.DashSpecDiagnostic(
                2,
                4,
                2,
                10,
                "unexpected token",
                DashSpec.Core.Validation.DashSpecDiagnosticSeverity.Warning)

        let modeling = DiagnosticInterop.fromLegacy legacy
        let roundTrip = DiagnosticInterop.toLegacy modeling

        Assert.Equal(legacy.Line, roundTrip.Line)
        Assert.Equal(legacy.Character, roundTrip.Character)
        Assert.Equal(legacy.EndLine, roundTrip.EndLine)
        Assert.Equal(legacy.EndCharacter, roundTrip.EndCharacter)
        Assert.Equal(legacy.Message, roundTrip.Message)
        Assert.Equal(legacy.Severity, roundTrip.Severity)

    [<Fact>]
    member _.``FSharp_diagnostic_factory_sets_error_severity`` () =
        let diagnostic =
            DashSpecDiagnostic.error 0 0 0 5 "missing end dashboard"

        match diagnostic.Severity with
        | DashSpecDiagnosticSeverity.Error -> ()
        | _ -> Assert.Fail("expected Error severity")

        Assert.Equal("missing end dashboard", diagnostic.Message)
