using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class TransformModuleParser
{
    public static SeriesTransformBlock ParseTransformFile(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (TransformParseBridge.ParseTransformFile is { } parse)
        {
            return parse(text);
        }

        throw new InvalidOperationException("Transform parse bridge not registered.");
    }
}
