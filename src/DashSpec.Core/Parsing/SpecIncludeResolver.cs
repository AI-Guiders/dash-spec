using DashSpec.Core.Authoring;
using DashSpec.Core.Model;
using DashSpec.Modeling.Parse.Include;

namespace DashSpec.Core.Parsing;

/// <summary>Resolved diagram / presentation / transform / tooltip fragment from an include file.</summary>
internal sealed record SpecIncludeFragment(
    DiagramDefinition? Diagram,
    PresentationBlock? Presentation,
    SeriesTransformBlock? SeriesTransform,
    IReadOnlyDictionary<string, TooltipDefinition>? Tooltips = null,
    InspectPresentation? Inspect = null);

/// <summary>Load/merge SSOT: <c>DashSpec.Modeling.Parse.Include.SpecIncludeFragmentResolver</c> (F#).</summary>
internal static class SpecIncludeResolver
{
    internal static void SetStdlibRootForTests(string? path) =>
        Authoring.SpecFragmentPaths.SetStdlibRootForTests(path);

    public static string ResolvePath(string reference, string specDirectory) =>
        Authoring.SpecFragmentPaths.ResolvePath(reference, specDirectory);

    public static SpecIncludeFragment Load(string includeKind, string reference, string specDirectory)
    {
        try
        {
            return SpecIncludeFragmentMapper.ToCore(
                SpecIncludeFragmentResolver.load(includeKind, reference, specDirectory));
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    public static SpecIncludeFragment Merge(SpecIncludeFragment current, SpecIncludeFragment incoming)
    {
        try
        {
            return SpecIncludeFragmentMapper.ToCore(
                SpecIncludeFragmentResolver.merge(
                    SpecIncludeFragmentMapper.ToFsharp(current),
                    SpecIncludeFragmentMapper.ToFsharp(incoming)));
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }
}
