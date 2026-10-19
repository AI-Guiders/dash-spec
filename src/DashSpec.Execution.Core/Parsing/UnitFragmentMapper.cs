using DashSpec.Execution.Runtime;
using Microsoft.FSharp.Core;
using FsharpDiagram = DashSpec.Modeling.Parse.Diagram.DiagramDefinition;
using FsharpFragment = DashSpec.Modeling.Parse.Import.UnitFragment;
using FsharpInspect = DashSpec.Modeling.Parse.Diagram.InspectPresentation;
using FsharpPresentation = DashSpec.Modeling.Parse.Presentation.PresentationBlock;
using FsharpTooltip = DashSpec.Modeling.Parse.Tooltip.TooltipDefinition;
using FsharpTransform = DashSpec.Modeling.Parse.Transform.SeriesTransformBlock;


namespace DashSpec.Execution.Parsing;

/// <summary>Maps F# import fragments to Core runtime models (ADR-0048).</summary>
internal static class UnitFragmentMapper
{
    public static UnitFragment ToCore(FsharpFragment fragment) =>
        new(
            FromFsharpOption(fragment.Diagram, ToCoreDiagram),
            FromFsharpOption(fragment.Presentation, ToCorePresentation),
            FromFsharpOption(fragment.SeriesTransform, ToCoreTransform),
            FromFsharpTooltips(fragment.Tooltips),
            FromFsharpOption(fragment.Inspect, ToCoreInspect));

    public static FsharpFragment ToFsharp(UnitFragment fragment) =>
        new()
        {
            Diagram = ToFsharpOption(fragment.Diagram, ToFsharpDiagram),
            Presentation = ToFsharpOption(fragment.Presentation, ToFsharpPresentation),
            SeriesTransform = ToFsharpOption(fragment.SeriesTransform, ToFsharpTransform),
            Tooltips = ToFsharpTooltips(fragment.Tooltips),
            Inspect = ToFsharpOption(fragment.Inspect, ToFsharpInspect),
        };

    public static PresentationBlock ToCorePresentation(FsharpPresentation block) =>
        block;

    private static DiagramDefinition ToCoreDiagram(FsharpDiagram diagram) =>
        diagram;

    private static SeriesTransformBlock ToCoreTransform(FsharpTransform transform) =>
        transform;

    private static TooltipDefinition ToCoreTooltip(FsharpTooltip tooltip)
    {
        var definition = new TooltipDefinition(tooltip.Id, tooltip.Variables, tooltip.Template);
        try
        {
            TooltipTemplate.Validate(definition);
        }
        catch (InvalidOperationException ex)
        {
            throw new DashSpecParseException(ex.Message);
        }

        return definition;
    }

    private static InspectPresentation ToCoreInspect(FsharpInspect inspect) =>
        inspect;

    private static FsharpDiagram ToFsharpDiagram(DiagramDefinition diagram) =>
        diagram;

    private static FsharpPresentation ToFsharpPresentation(PresentationBlock block) =>
        block;

    private static FsharpTransform ToFsharpTransform(SeriesTransformBlock transform) =>
        transform;

    private static FsharpTooltip ToFsharpTooltip(TooltipDefinition tooltip) =>
        new()
        {
            Id = tooltip.Id,
            Variables = tooltip.Variables,
            Template = tooltip.Template,
        };

    private static FsharpInspect ToFsharpInspect(InspectPresentation inspect) =>
        inspect;

    private static TCore? FromFsharpOption<TFsharp, TCore>(FSharpOption<TFsharp> option, Func<TFsharp, TCore> map) =>
        FSharpOption<TFsharp>.get_IsSome(option) ? map(option.Value) : default;

    private static FSharpOption<TFsharp> ToFsharpOption<TCore, TFsharp>(TCore? value, Func<TCore, TFsharp> map) =>
        value is null ? FSharpOption<TFsharp>.None : FSharpOption<TFsharp>.Some(map(value));

    private static FSharpOption<string> ToFsharpStringOption(string? value) =>
        string.IsNullOrWhiteSpace(value) ? FSharpOption<string>.None : FSharpOption<string>.Some(value);

    private static IReadOnlyDictionary<string, TooltipDefinition>? FromFsharpTooltips(
        FSharpOption<IReadOnlyDictionary<string, FsharpTooltip>> tooltips)
    {
        if (!FSharpOption<IReadOnlyDictionary<string, FsharpTooltip>>.get_IsSome(tooltips))
        {
            return null;
        }

        var map = tooltips.Value;
        if (map.Count == 0)
        {
            return null;
        }

        return map.ToDictionary(x => x.Key, x => ToCoreTooltip(x.Value), StringComparer.OrdinalIgnoreCase);
    }

    private static FSharpOption<IReadOnlyDictionary<string, FsharpTooltip>> ToFsharpTooltips(
        IReadOnlyDictionary<string, TooltipDefinition>? tooltips)
    {
        if (tooltips is null || tooltips.Count == 0)
        {
            return FSharpOption<IReadOnlyDictionary<string, FsharpTooltip>>.None;
        }

        var map = tooltips.ToDictionary(
            x => x.Key,
            x => ToFsharpTooltip(x.Value),
            StringComparer.OrdinalIgnoreCase);
        return FSharpOption<IReadOnlyDictionary<string, FsharpTooltip>>.Some(map);
    }
}
