using DashSpec.Core.Model;
using DashSpec.Core.Runtime;
using Microsoft.FSharp.Core;
using FsharpDiagram = DashSpec.Modeling.Parse.Diagram.DiagramDefinition;
using FsharpFragment = DashSpec.Modeling.Parse.Include.SpecIncludeFragment;
using FsharpInspect = DashSpec.Modeling.Parse.Diagram.InspectPresentation;
using FsharpPresentation = DashSpec.Modeling.Parse.Presentation.PresentationBlock;
using FsharpTooltip = DashSpec.Modeling.Parse.Tooltip.TooltipDefinition;
using FsharpTransform = DashSpec.Modeling.Parse.Transform.SeriesTransformBlock;

namespace DashSpec.Core.Parsing;

/// <summary>Maps F# include fragments to Core runtime models (ADR-0048).</summary>
internal static class SpecIncludeFragmentMapper
{
    public static SpecIncludeFragment ToCore(FsharpFragment fragment) =>
        new(
            FromFsharpOption(fragment.Diagram, ToCoreDiagram),
            FromFsharpOption(fragment.Presentation, ToCorePresentation),
            FromFsharpOption(fragment.SeriesTransform, ToCoreTransform),
            FromFsharpTooltips(fragment.Tooltips),
            FromFsharpOption(fragment.Inspect, ToCoreInspect));

    public static FsharpFragment ToFsharp(SpecIncludeFragment fragment) =>
        new()
        {
            Diagram = ToFsharpOption(fragment.Diagram, ToFsharpDiagram),
            Presentation = ToFsharpOption(fragment.Presentation, ToFsharpPresentation),
            SeriesTransform = ToFsharpOption(fragment.SeriesTransform, ToFsharpTransform),
            Tooltips = ToFsharpTooltips(fragment.Tooltips),
            Inspect = ToFsharpOption(fragment.Inspect, ToFsharpInspect),
        };

    public static PresentationBlock ToCorePresentation(FsharpPresentation block) =>
        new(
            OptionModule.ToArray(block.UsePreset).FirstOrDefault(),
            block.Properties.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase));

    private static DiagramDefinition ToCoreDiagram(FsharpDiagram diagram)
    {
        var usePreset = OptionModule.ToArray(diagram.UsePreset).FirstOrDefault();
        return new DiagramDefinition(
            diagram.Kind,
            diagram.Properties.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            usePreset);
    }

    private static SeriesTransformBlock ToCoreTransform(FsharpTransform transform) =>
        new(
            OptionModule.ToArray(transform.UsePreset).FirstOrDefault(),
            OptionModule.ToArray(transform.Max).FirstOrDefault(),
            OptionModule.ToArray(transform.OtherLabel).FirstOrDefault());

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
        new(
            OptionModule.ToArray(inspect.TooltipId).FirstOrDefault(),
            OptionModule.ToArray(inspect.Label).FirstOrDefault(),
            inspect.Format,
            inspect.Split);

    private static FsharpDiagram ToFsharpDiagram(DiagramDefinition diagram) =>
        new()
        {
            Kind = diagram.Kind,
            Properties = diagram.Properties,
            UsePreset = ToFsharpStringOption(diagram.UsePreset),
        };

    private static FsharpPresentation ToFsharpPresentation(PresentationBlock block) =>
        new()
        {
            UsePreset = ToFsharpStringOption(block.UsePreset),
            Properties = block.Properties,
        };

    private static FsharpTransform ToFsharpTransform(SeriesTransformBlock transform) =>
        new()
        {
            UsePreset = ToFsharpStringOption(transform.UsePreset),
            Max = transform.Max is int max ? FSharpOption<int>.Some(max) : FSharpOption<int>.None,
            OtherLabel = ToFsharpStringOption(transform.OtherLabel),
        };

    private static FsharpTooltip ToFsharpTooltip(TooltipDefinition tooltip) =>
        new()
        {
            Id = tooltip.Id,
            Variables = tooltip.Variables,
            Template = tooltip.Template,
        };

    private static FsharpInspect ToFsharpInspect(InspectPresentation inspect) =>
        new()
        {
            TooltipId = ToFsharpStringOption(inspect.TooltipId),
            Label = ToFsharpStringOption(inspect.Label),
            Format = inspect.Format,
            Split = inspect.Split,
        };

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
