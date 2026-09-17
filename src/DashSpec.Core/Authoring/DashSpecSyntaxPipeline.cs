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
}
