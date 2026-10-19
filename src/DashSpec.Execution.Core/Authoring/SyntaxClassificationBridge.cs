namespace DashSpec.Execution.Authoring;

internal static class SyntaxClassificationBridge
{
    public static Func<string, IReadOnlyList<DashSpecSyntaxSpan>>? Classify;
}
