using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class PresentationParseBridge
{
    internal static Func<string, string?, PresentationBlock>? ParsePresentationFile { get; set; }

    internal static Func<string, string?, (string Id, PresentationBlock Block)>? ParsePresentationFileWithId { get; set; }
}
