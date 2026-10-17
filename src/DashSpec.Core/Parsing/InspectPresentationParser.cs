using DashSpec.Core.Runtime;
using Microsoft.FSharp.Core;
using FsharpInspect = DashSpec.Modeling.Parse.Diagram.InspectPresentation;

namespace DashSpec.Core.Parsing;

/// <summary>
/// Runtime helpers for inspect chrome. Parse/merge SSOT: <c>DashSpec.Modeling.Parse.Diagram.InspectPresentationParser</c> (F#).
/// </summary>
internal static class InspectPresentationParser
{
    public static InspectPresentation? Merge(InspectPresentation? left, InspectPresentation? right)
    {
        var merged = Modeling.Parse.Diagram.InspectPresentationParser.merge(
            ToFsharpOption(left),
            ToFsharpOption(right));
        return ToCore(merged);
    }

    public static TooltipFormat ToTooltipFormat(InspectPresentation? inspect) =>
        TooltipFormatParser.Parse(inspect?.Format, TooltipFormat.Inline);

    private static FSharpOption<FsharpInspect> ToFsharpOption(InspectPresentation? inspect) =>
        inspect is null ? FSharpOption<FsharpInspect>.None : FSharpOption<FsharpInspect>.Some(ToFsharp(inspect));

    private static FsharpInspect ToFsharp(InspectPresentation inspect) =>
        inspect;

    private static InspectPresentation? ToCore(FSharpOption<FsharpInspect> inspect) =>
        FSharpOption<FsharpInspect>.get_IsSome(inspect) ? ToCore(inspect.Value) : null;

    private static InspectPresentation ToCore(FsharpInspect inspect) =>
        inspect;
}
