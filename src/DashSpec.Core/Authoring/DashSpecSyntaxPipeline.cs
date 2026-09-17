namespace DashSpec.Core.Authoring;

public static class DashSpecSyntaxPipeline
{
    public static IReadOnlyList<DashSpecSyntaxSpan> Classify(string text)
    {
        if (SyntaxClassificationBridge.Classify is { } classify)
        {
            return classify(text);
        }

        throw new InvalidOperationException(
            "DashSpec syntax bridge not registered. Reference DashSpec.Execution.Core or call ModuleParseRegistration.EnsureRegistered().");
    }

    public static DashSpecSyntaxTree Parse(string text)
    {
        if (SyntaxTreeBridge.Parse is { } parse)
        {
            return parse(text);
        }

        throw new InvalidOperationException(
            "DashSpec syntax tree bridge not registered. Reference DashSpec.Execution.Core or call ModuleParseRegistration.EnsureRegistered().");
    }
}
