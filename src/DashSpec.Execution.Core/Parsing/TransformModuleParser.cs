namespace DashSpec.Execution.Parsing;

internal static class TransformModuleParser
{
    public static SeriesTransformBlock ParseTransformFile(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return ModuleParseRegistration.ParseTransformFile(text);
    }
}
